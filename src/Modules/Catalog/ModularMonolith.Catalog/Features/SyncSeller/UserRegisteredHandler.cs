using Dapper;
using ModularMonolith.Identity.Contracts;
using ModularMonolith.Shared.Infrastructure.Data;
using ModularMonolith.Shared.Kernel.Messaging;

namespace ModularMonolith.Catalog.Features.SyncSeller;

/// <summary>
/// Cross-module consumer: keeps Catalog's local <c>sellers</c> read model in sync with Identity,
/// so Catalog never queries Identity's tables. Runs in the event's tenant, inside an
/// inbox-protected transaction, so redelivery is harmless.
/// </summary>
internal sealed class UserRegisteredHandler(IDbSession session) : IIntegrationEventHandler<UserRegisteredIntegrationEvent>
{
    public async Task HandleAsync(UserRegisteredIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        var connection = await session.GetConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            insert into catalog.sellers (subject_id, display_name, email, registered_at)
            values (@SubjectId, @DisplayName, @Email, @OccurredAt)
            on conflict (tenant_id, subject_id)
            do update set display_name = excluded.display_name, email = excluded.email
            """,
            new { integrationEvent.SubjectId, integrationEvent.DisplayName, integrationEvent.Email, integrationEvent.OccurredAt },
            session.Transaction,
            cancellationToken: cancellationToken));
    }
}
