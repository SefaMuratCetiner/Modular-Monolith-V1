using Dapper;
using ModularMonolith.Identity.Contracts;
using ModularMonolith.Shared.Infrastructure.Data;
using ModularMonolith.Shared.Infrastructure.Privacy;
using ModularMonolith.Shared.Kernel.Messaging;

namespace ModularMonolith.Catalog.Features.ErasePersonalData;

/// <summary>
/// Right to erasure, Catalog's part: anonymizes the seller projection of an erased user. Products keep
/// their <c>seller_id</c> (a pseudonymous subject id), so listings stay consistent without naming anyone.
/// Inbox-protected and idempotent.
/// </summary>
internal sealed class UserErasedIntegrationEventHandler(IDbSession session) : IIntegrationEventHandler<UserErasedIntegrationEvent>
{
    public async Task HandleAsync(UserErasedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        var connection = await session.GetConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            update catalog.sellers
            set display_name = @ErasedName, erased_at = @OccurredAt
            where subject_id = @SubjectId
            """,
            new { ErasedName = PersonalDataRedaction.ErasedDisplayName, integrationEvent.OccurredAt, integrationEvent.SubjectId },
            session.Transaction,
            cancellationToken: cancellationToken));
    }
}
