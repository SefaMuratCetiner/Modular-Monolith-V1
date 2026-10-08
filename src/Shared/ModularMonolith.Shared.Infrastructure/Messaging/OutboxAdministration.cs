using Dapper;
using Microsoft.Extensions.Logging;
using ModularMonolith.Shared.Infrastructure.Data;
using ModularMonolith.Shared.Infrastructure.Modules;
using ModularMonolith.Shared.Infrastructure.Observability;
using ModularMonolith.Shared.Kernel.Paging;
using ModularMonolith.Shared.Kernel.Results;
using Npgsql;

namespace ModularMonolith.Shared.Infrastructure.Messaging;

/// <summary>
/// Operator tooling for dead-lettered outbox messages: inspect them and replay them once the cause
/// is fixed. Replays are safe because every consumer is protected by the inbox.
/// </summary>
public sealed class OutboxAdministration(
    NpgsqlDataSource dataSource,
    ModuleCatalog modules,
    OutboxSignal signal,
    ILogger<OutboxAdministration> logger)
{
    private const int MaxErrorPreviewLength = 500;

    private static readonly Error UnknownModule = Error.NotFound("outbox.unknown_module", "No module owns this schema.");
    private static readonly Error MessageNotFound = Error.NotFound("outbox.dead_letter_not_found", "No dead-lettered message with this id exists.");

    /// <summary>Lists one page of dead-lettered messages of a module, oldest first.</summary>
    /// <param name="schema">The module schema.</param>
    /// <param name="paging">The page.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The page, or not-found for an unknown module.</returns>
    public async Task<Result<PagedResult<DeadLetter>>> ListDeadLettersAsync(string schema, PageRequest paging, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(paging);
        if (Table(schema) is not { } table)
        {
            return Result.Failure<PagedResult<DeadLetter>>(UnknownModule);
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var total = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            $"select count(*) from {table} where status = 'dead'",
            cancellationToken: cancellationToken));
        var rows = await connection.QueryAsync<DeadLetter>(new CommandDefinition(
            $"""
            select id as Id, type as Type, tenant_id as TenantId, occurred_at as OccurredAt, attempts as Attempts,
                   left(split_part(coalesce(error, ''), E'\n', 1), {MaxErrorPreviewLength}) as Error
            from {table}
            where status = 'dead'
            order by occurred_at, id
            limit @Limit offset @Offset
            """,
            new { Limit = paging.PageSize, paging.Offset },
            cancellationToken: cancellationToken));

        return Result.Success(PagedResult.Create(paging, rows.AsList(), total));
    }

    /// <summary>Re-queues one dead-lettered message (or all of them when <paramref name="messageId"/> is null).</summary>
    /// <param name="schema">The module schema.</param>
    /// <param name="messageId">The message id, or <see langword="null"/> for all dead letters.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of re-queued messages.</returns>
    public async Task<Result<int>> RequeueAsync(string schema, Guid? messageId, CancellationToken cancellationToken)
    {
        if (Table(schema) is not { } table)
        {
            return Result.Failure<int>(UnknownModule);
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var requeued = await connection.ExecuteAsync(new CommandDefinition(
            $"""
            update {table}
            set status = 'pending', attempts = 0, next_attempt_at = now(), locked_until = null, error = null
            where status = 'dead' and (@Id::uuid is null or id = @Id)
            """,
            new { Id = messageId },
            cancellationToken: cancellationToken));

        if (messageId is not null && requeued == 0)
        {
            return Result.Failure<int>(MessageNotFound);
        }

        logger.DeadLettersRequeued(requeued, schema);
        signal.Notify(schema);
        return Result.Success(requeued);
    }

    private string? Table(string schema)
        => modules.FindBySchema(schema) is { } module ? $"{SqlIdentifier.Quote(module.Schema)}.outbox_messages" : null;
}

/// <summary>A dead-lettered outbox message (operator view; payloads are not exposed).</summary>
/// <param name="Id">The message id.</param>
/// <param name="Type">The integration event type name.</param>
/// <param name="TenantId">The tenant.</param>
/// <param name="OccurredAt">When the event occurred.</param>
/// <param name="Attempts">Delivery attempts made.</param>
/// <param name="Error">The first line of the last error (truncated).</param>
public sealed record DeadLetter(Guid Id, string Type, string TenantId, DateTimeOffset OccurredAt, int Attempts, string Error);
