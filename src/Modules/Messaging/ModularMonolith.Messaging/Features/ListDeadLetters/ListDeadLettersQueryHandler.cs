using ModularMonolith.Shared.Infrastructure.Messaging;
using ModularMonolith.Shared.Kernel.Cqrs;
using ModularMonolith.Shared.Kernel.Paging;
using ModularMonolith.Shared.Kernel.Results;

namespace ModularMonolith.Messaging.Features.ListDeadLetters;

/// <summary>Reads dead letters through the outbox's administration API (payloads are never exposed).</summary>
internal sealed class ListDeadLettersQueryHandler(OutboxAdministration administration)
    : IQueryHandler<ListDeadLettersQuery, PagedResult<DeadLetter>>
{
    public Task<Result<PagedResult<DeadLetter>>> HandleAsync(ListDeadLettersQuery query, CancellationToken cancellationToken)
        => administration.ListDeadLettersAsync(query.Module, query.Paging, cancellationToken);
}
