using Dapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModularMonolith.Shared.Infrastructure.Data;
using ModularMonolith.Shared.Infrastructure.Modules;
using ModularMonolith.Shared.Infrastructure.Observability;
using Npgsql;

namespace ModularMonolith.Shared.Infrastructure.Messaging;

/// <summary>
/// Drains one module's outbox. Invoked by the in-process dispatcher right after commits and by the
/// recurring Hangfire sweep as a safety net.
/// <list type="bullet">
/// <item><b>Lease, don't lock:</b> a short transaction claims a batch (<c>FOR UPDATE SKIP LOCKED</c>) and
/// marks it <c>processing</c> with a lease; consumers then run with no outbox transaction or row lock
/// held. A crashed worker's lease expires and the batch is reclaimed.</item>
/// <item>Any number of replicas can run it concurrently.</item>
/// <item>The outbox table has no RLS: one run drains every tenant; each consumer runs in the message's tenant.</item>
/// <item>Failures back off exponentially and are dead-lettered after <see cref="OutboxOptions.MaxAttempts"/>.</item>
/// <item>The producer's trace context travels with the message, so consumer spans join its trace.</item>
/// </list>
/// </summary>
public sealed class OutboxProcessor(
    NpgsqlDataSource dataSource,
    ModuleCatalog modules,
    IntegrationEventRegistry registry,
    InProcessEventBus eventBus,
    IOptions<OutboxOptions> options,
    TimeProvider timeProvider,
    ILogger<OutboxProcessor> logger)
{
    private const int MaxErrorLength = 4000;

    /// <summary>Processes pending messages of a module's outbox.</summary>
    /// <param name="schema">The module schema.</param>
    /// <param name="cancellationToken">The cancellation token (supplied by Hangfire on shutdown).</param>
    /// <returns>A task.</returns>
    public async Task ProcessAsync(string schema, CancellationToken cancellationToken)
    {
        var module = modules.FindBySchema(schema)
            ?? throw new ArgumentException($"'{schema}' is not a registered module schema.", nameof(schema));
        var table = $"{SqlIdentifier.Quote(module.Schema)}.outbox_messages";
        var settings = options.Value;

        for (var batch = 0; batch < settings.MaxBatchesPerRun; batch++)
        {
            var claimed = await ClaimBatchAsync(table, settings, cancellationToken);
            foreach (var message in claimed)
            {
                await DispatchAsync(table, module.Schema, message, settings, cancellationToken);
            }

            if (claimed.Count < settings.BatchSize)
            {
                break;
            }
        }
    }

    private async Task<List<OutboxRow>> ClaimBatchAsync(string table, OutboxOptions settings, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var sql = $"""
            with claimable as (
                select id from {table}
                where (status = 'pending' and next_attempt_at <= @Now)
                   or (status = 'processing' and locked_until < @Now)
                order by occurred_at
                limit @BatchSize
                for update skip locked)
            update {table} t
            set status = 'processing', locked_until = @LockedUntil
            from claimable
            where t.id = claimable.id
            returning t.id as Id, t.type as Type, t.payload::text as Payload, t.attempts as Attempts,
                      t.trace_parent as TraceParent, t.occurred_at as OccurredAt
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<OutboxRow>(new CommandDefinition(
            sql,
            new { Now = now, settings.BatchSize, LockedUntil = now.AddSeconds(settings.LeaseSeconds) },
            cancellationToken: cancellationToken));

        // RETURNING has no order guarantee; keep delivery in occurrence order within the batch.
        return [.. rows.OrderBy(row => row.OccurredAt)];
    }

    private async Task DispatchAsync(string table, string schema, OutboxRow message, OutboxOptions settings, CancellationToken cancellationToken)
    {
        var tag = new KeyValuePair<string, object?>("module", schema);
        try
        {
            var integrationEvent = registry.Deserialize(message.Type, message.Payload);
            await eventBus.PublishAsync(integrationEvent, message.TraceParent, cancellationToken);
            await ExecuteAsync(
                $"update {table} set status = 'processed', processed_at = @Now, locked_until = null, error = null where id = @Id",
                new { Now = timeProvider.GetUtcNow(), message.Id },
                cancellationToken);
            Telemetry.OutboxProcessed.Add(1, tag);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await RecordFailureAsync(table, message, exception, settings, tag, cancellationToken);
        }
    }

    private async Task RecordFailureAsync(
        string table,
        OutboxRow message,
        Exception exception,
        OutboxOptions settings,
        KeyValuePair<string, object?> tag,
        CancellationToken cancellationToken)
    {
        var attempts = message.Attempts + 1;
        var dead = attempts >= settings.MaxAttempts;
        var nextAttemptAt = timeProvider.GetUtcNow() + Backoff(attempts, settings);
        await ExecuteAsync(
            $"""
            update {table}
            set attempts = @Attempts, error = @Error, next_attempt_at = @NextAttemptAt, locked_until = null,
                status = case when @Dead then 'dead' else 'pending' end
            where id = @Id
            """,
            new { Attempts = attempts, Error = Truncate(exception.ToString()), NextAttemptAt = nextAttemptAt, Dead = dead, message.Id },
            cancellationToken);

        if (dead)
        {
            Telemetry.OutboxDeadLettered.Add(1, tag);
            logger.OutboxDeadLettered(exception, message.Id, attempts);
        }
        else
        {
            Telemetry.OutboxFailed.Add(1, tag);
            logger.OutboxRetry(exception, message.Id, attempts, nextAttemptAt);
        }
    }

    private async Task ExecuteAsync(string sql, object parameters, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    private static TimeSpan Backoff(int attempts, OutboxOptions settings)
    {
        var seconds = Math.Min(settings.BaseDelaySeconds * Math.Pow(2, attempts - 1), settings.MaxDelaySeconds);
        return TimeSpan.FromSeconds(seconds);
    }

    private static string Truncate(string value) => value.Length <= MaxErrorLength ? value : value[..MaxErrorLength];

    /// <summary>Dapper projection of a claimed outbox row.</summary>
    private sealed class OutboxRow
    {
        public Guid Id { get; init; }

        public string Type { get; init; } = string.Empty;

        public string Payload { get; init; } = string.Empty;

        public int Attempts { get; init; }

        public string? TraceParent { get; init; }

        public DateTimeOffset OccurredAt { get; init; }
    }
}
