using Dapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModularMonolith.Shared.Infrastructure.Data;
using ModularMonolith.Shared.Infrastructure.Modules;
using ModularMonolith.Shared.Infrastructure.Observability;
using Npgsql;

namespace ModularMonolith.Shared.Infrastructure.Messaging;

/// <summary>
/// Daily Hangfire job that applies the retention policy to a module's system tables:
/// <list type="bullet">
/// <item>outbox: delivered messages and dead letters;</item>
/// <item>inbox: deduplication records;</item>
/// <item>audit log: removes client IP addresses after <c>AuditIpAddressDays</c> and whole rows after
/// <c>AuditLogDays</c>. The application role stays append-only on <c>audit_log</c> (no UPDATE or DELETE
/// grant): it can only call the module's <c>purge_audit_log</c> function, a SECURITY DEFINER function owned
/// by the schema owner that touches nothing younger than 30 days.</item>
/// </list>
/// Deletes run in small batches to keep locks short.
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

        if (settings.DeadLetterDays > 0)
        {
            await PurgeAsync($"{quoted}.outbox_messages", "status = 'dead' and occurred_at < @Cutoff", now.AddDays(-settings.DeadLetterDays), settings.BatchSize, cancellationToken);
        }

        if (settings.InboxDays > 0)
        {
            await PurgeAsync($"{quoted}.inbox_messages", "processed_at < @Cutoff", now.AddDays(-settings.InboxDays), settings.BatchSize, cancellationToken);
        }

        if (settings.AuditLogDays > 0 || settings.AuditIpAddressDays > 0)
        {
            await PurgeAuditLogAsync(
                quoted,
                settings.AuditLogDays > 0 ? now.AddDays(-settings.AuditLogDays) : null,
                settings.AuditIpAddressDays > 0 ? now.AddDays(-settings.AuditIpAddressDays) : null,
                settings.BatchSize,
                cancellationToken);
        }
    }

    private async Task PurgeAuditLogAsync(string quotedSchema, DateTimeOffset? deleteBefore, DateTimeOffset? stripIpBefore, int batchSize, CancellationToken cancellationToken)
    {
        // Each call deletes and anonymizes at most one batch each; repeat until nothing is left.
        var table = $"{quotedSchema}.audit_log";
        var sql = $"select {quotedSchema}.purge_audit_log(@DeleteBefore, @StripIpBefore, @BatchSize)";
        long total = 0;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        while (true)
        {
            var affected = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                sql,
                new { DeleteBefore = deleteBefore, StripIpBefore = stripIpBefore, BatchSize = batchSize },
                cancellationToken: cancellationToken));
            total += affected;
            if (affected == 0)
            {
                break;
            }
        }

        logger.RetentionPurged(total, table);
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
