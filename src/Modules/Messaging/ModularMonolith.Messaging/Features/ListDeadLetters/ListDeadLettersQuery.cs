using ModularMonolith.Shared.Infrastructure.Messaging;
using ModularMonolith.Shared.Kernel.Cqrs;
using ModularMonolith.Shared.Kernel.Paging;

namespace ModularMonolith.Messaging.Features.ListDeadLetters;

/// <summary>Lists one page of a module's dead-lettered outbox messages (all tenants), oldest first.</summary>
/// <param name="Module">The module schema, e.g. <c>catalog</c>.</param>
/// <param name="Paging">The page.</param>
internal sealed record ListDeadLettersQuery(string Module, PageRequest Paging) : IQuery<PagedResult<DeadLetter>>;
