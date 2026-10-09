using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModularMonolith.Identity.Contracts;
using ModularMonolith.Identity.Infrastructure.Keycloak;
using ModularMonolith.Shared.Kernel.Messaging;
using Refit;

namespace ModularMonolith.Identity.Features.EraseUser;

/// <summary>
/// Right to erasure, identity-provider part: deletes (or disables and logs out) the user's Keycloak account
/// through the admin REST API. Identity consumes its own <see cref="UserErasedIntegrationEvent"/> instead of
/// calling Keycloak inside the erase command, so the HTTP call is outside the database transaction, a
/// Keycloak outage only delays it (outbox retries with backoff, then a dead letter an operator can replay),
/// and it runs exactly once (inbox). An account that is already gone (404) counts as success.
/// </summary>
internal sealed partial class UserErasedIntegrationEventHandler(
    IKeycloakAdminApi keycloak,
    IOptions<IdentityOptions> options,
    ILogger<UserErasedIntegrationEventHandler> logger) : IIntegrationEventHandler<UserErasedIntegrationEvent>
{
    public async Task HandleAsync(UserErasedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (!settings.AccountErasure.Enabled)
        {
            LogSkipped(logger, integrationEvent.SubjectId);
            return;
        }

        var realm = settings.Realm!;
        var userId = integrationEvent.SubjectId;
        if (settings.AccountErasure.Mode == AccountErasureMode.Delete)
        {
            // Deleting the account also ends its sessions and removes credentials and the OTP secret.
            using var deleted = await keycloak.DeleteUserAsync(realm, userId, cancellationToken);
            EnsureSucceeded(deleted, "delete");
        }
        else
        {
            using var disabled = await keycloak.UpdateUserAsync(realm, userId, new KeycloakUserUpdate(Enabled: false), cancellationToken);
            EnsureSucceeded(disabled, "disable");
            if (disabled.StatusCode != HttpStatusCode.NotFound)
            {
                using var loggedOut = await keycloak.LogoutAsync(realm, userId, cancellationToken);
                EnsureSucceeded(loggedOut, "logout");
            }
        }

        LogAccountErased(logger, userId, settings.AccountErasure.Mode);
    }

    private static void EnsureSucceeded(IApiResponse response, string operation)
    {
        if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.NotFound)
        {
            // Thrown so the outbox retries; the message is dead-lettered if Keycloak keeps refusing.
            throw new HttpRequestException(
                $"Keycloak admin API '{operation}' returned {(int)response.StatusCode}.",
                response.Error,
                response.StatusCode);
        }
    }

    [LoggerMessage(EventId = 3100, Level = LogLevel.Information, Message = "Keycloak account of subject {SubjectId} erased ({Mode})")]
    private static partial void LogAccountErased(ILogger logger, string subjectId, AccountErasureMode mode);

    [LoggerMessage(EventId = 3101, Level = LogLevel.Warning, Message = "Keycloak account of subject {SubjectId} NOT erased: Modules:Identity:AccountErasure:Enabled is false")]
    private static partial void LogSkipped(ILogger logger, string subjectId);
}
