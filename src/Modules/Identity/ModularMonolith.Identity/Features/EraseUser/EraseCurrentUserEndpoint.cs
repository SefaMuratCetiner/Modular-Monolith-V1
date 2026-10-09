using FastEndpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using ModularMonolith.Identity.Domain;
using ModularMonolith.Shared.Infrastructure.Web;
using ModularMonolith.Shared.Kernel.Context;
using EraseUserHandler = ModularMonolith.Shared.Kernel.Cqrs.ICommandHandler<
    ModularMonolith.Identity.Features.EraseUser.EraseUserCommand, ModularMonolith.Identity.Domain.UserProfileId>;

namespace ModularMonolith.Identity.Features.EraseUser;

/// <summary><c>DELETE /api/v1/identity/users/me</c>: erases the caller's own personal data (self-service).</summary>
internal sealed class EraseCurrentUserEndpoint(EraseUserHandler handler, ICurrentUser currentUser) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Delete("identity/users/me");
        Version(1);
        Policies(IdentityPermissions.ProfileWrite.Name);
        Options(builder => builder.WithTags("Identity").RequireRateLimiting(RateLimitPolicies.PerUser));
        Description(
            builder => builder
                .Produces(StatusCodes.Status204NoContent)
                .ProducesProblems(StatusCodes.Status403Forbidden, StatusCodes.Status404NotFound, StatusCodes.Status409Conflict),
            clearDefaults: true);
        Summary(summary =>
        {
            summary.Summary = "Erases the authenticated user's personal data in every module.";
            summary.Description = "Identity anonymizes the profile at once; other modules follow through the identity.user-erased event. The Keycloak account itself is managed in Keycloak.";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (currentUser.UserId is not { } subjectId)
        {
            await Send.ResultAsync(IdentityErrors.NotAuthenticated.ToProblem());
            return;
        }

        var result = await handler.HandleAsync(new EraseUserCommand(subjectId), ct);
        if (result.IsFailure)
        {
            await Send.ResultAsync(result.Error.ToProblem());
            return;
        }

        await Send.NoContentAsync(ct);
    }
}
