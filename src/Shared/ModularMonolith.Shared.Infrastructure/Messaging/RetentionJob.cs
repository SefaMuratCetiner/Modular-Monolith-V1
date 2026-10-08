using Dapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModularMonolith.Shared.Infrastructure.Data;
using ModularMonolith.Shared.Infrastructure.Modules;
using ModularMonolith.Shared.Infrastructure.Observability;
using Npgsql;

namespace ModularMonolith.Shared.Infrastructure.Messaging;

/// <summary>
/// Daily Hangfire job that purges old rows from a module's outbox (delivered messages only),
/// inbox and, when configured, audit log. Deletes run in small batches to keep locks short.
/// </summary>
public sealed class RetentionJob(
    NpgsqlDataSource dataSource,
    ModuleCatalog modules,
    IOptions<RetentionOptions> options,
    TimeProvider timeProvider,
    ILogger<RetentionJob> logger)
{
    /// <summary>Applies the retention policy to one module schema.</summary>
    /// <param name="schema">The module schema.</param>
    /// <param name="cancellationToken">The cancellation token (supplied by Hangfire on shutdown).</param>
    /// <returns>A task.</returns>
    public async Task PurgeAsync(string schema, CancellationToken cancellationToken)
    {
        var module = modules.FindBySchema(schema)
            ?? throw new ArgumentException($"'{schema}' is not a registered module schema.", nameof(schema));
        var quoted = SqlIdentifier.Quote(module.Schema);
        var settings = options.Value;
        var now = timeProvider.GetUtcNow();

        if (settings.ProcessedOutboxDays > 0)
        {
            await PurgeAsync($"{quoted}.outbox_messages", "status = 'processed' and processed_at < @Cutoff", now.AddDays(-settings.ProcessedOutboxDays), settings.BatchSize, cancellationToken);
        }

        if (settings.InboxDays > 0)
        {
            await PurgeAsync($"{quoted}.inbox_messages", "processed_at < @Cutoff", now.AddDays(-settings.InboxDays), settings.BatchSize, cancellationToken);
        }

        if (settings.AuditDays > 0)
        {
            await PurgeAsync($"{quoted}.audit_log", "occurred_at < @Cutoff", now.AddDays(-settings.AuditDays), settings.BatchSize, cancellationToken);
        }
    }

    private async Task PurgeAsync(string table, string predicate, DateTimeOffset cutoff, int batchSize, CancellationToken cancellationToken)
    {
        var sql = $"delete from {table} where ctid in (select ctid from {table} where {predicate} limit @BatchSize)";
        long total = 0;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        while (true)
        {
            var deleted = await connection.ExecuteAsync(new CommandDefinition(
                sql,
                new { Cutoff = cutoff, BatchSize = batchSize },
                cancellationToken: cancellationToken));
            total += deleted;
            if (deleted < batchSize)
            {
                break;
            }
        }

        logger.RetentionPurged(total, table);
    }
}
