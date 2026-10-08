using Dapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModularMonolith.Shared.Infrastructure.Data;
using ModularMonolith.Shared.Infrastructure.Modules;
using ModularMonolith.Shared.Infrastructure.Observability;
using Npgsql;

namespace ModularMonolith.Shared.Infrastructure.Messaging;

/// <summary>
/// Hangfire job that drains one module's outbox.
/// <list type="bullet">
/// <item>Rows are claimed with <c>FOR UPDATE SKIP LOCKED</c>, so any number of replicas can run it concurrently.</item>
/// <item>The outbox table has no RLS: one run drains every tenant; each consumer runs in the message's tenant.</item>
/// <item>Failures back off exponentially and are dead-lettered after <see cref="OutboxOptions.MaxAttempts"/>.</item>
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
            var processed = await ProcessBatchAsync(table, module.Schema, settings, cancellationToken);
            if (processed < settings.BatchSize)
            {
                break;
            }
        }
    }

    private async Task<int> ProcessBatchAsync(string table, string schema, OutboxOptions settings, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();

        var claimSql = $"""
            select id, type, payload::text as payload, attempts
            from {table}
            where status = 'pending' and next_attempt_at <= @Now
            order by occurred_at
            limit @BatchSize
            for update skip locked
            """;
        var messages = (await connection.QueryAsync<OutboxRow>(new CommandDefinition(
            claimSql,
            new { Now = now, settings.BatchSize },
            transaction,
            cancellationToken: cancellationToken))).AsList();

        var tag = new KeyValuePair<string, object?>("module", schema);
        foreach (var message in messages)
        {
            try
            {
                var integrationEvent = registry.Deserialize(message.Type, message.Payload);
                await eventBus.PublishAsync(integrationEvent, cancellationToken);
                await connection.ExecuteAsync(new CommandDefinition(
                    $"update {table} set status = 'processed', processed_at = @Now, error = null where id = @Id",
                    new { Now = timeProvider.GetUtcNow(), message.Id },
                    transaction,
                    cancellationToken: cancellationToken));
                Telemetry.OutboxProcessed.Add(1, tag);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                await RecordFailureAsync(connection, transaction, table, message, exception, settings, tag, cancellationToken);
            }
        }

        await transaction.CommitAsync(cancellationToken);
        return messages.Count;
    }

    private async Task RecordFailureAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
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
        await connection.ExecuteAsync(new CommandDefinition(
            $"""
            update {table}
            set attempts = @Attempts, error = @Error, next_attempt_at = @NextAttemptAt,
                status = case when @Dead then 'dead' else 'pending' end
            where id = @Id
            """,
            new { Attempts = attempts, Error = Truncate(exception.ToString()), NextAttemptAt = nextAttemptAt, Dead = dead, message.Id },
            transaction,
            cancellationToken: cancellationToken));

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

    private static TimeSpan Backoff(int attempts, OutboxOptions settings)
    {
        var seconds = Math.Min(settings.BaseDelaySeconds * Math.Pow(2, attempts - 1), settings.MaxDelaySeconds);
        return TimeSpan.FromSeconds(seconds);
    }

    private static string Truncate(string value) => value.Length <= MaxErrorLength ? value : value[..MaxErrorLength];

    /// <summary>Dapper projection of a claimed outbox row (constructor order matches the select list).</summary>
    private sealed record OutboxRow(Guid Id, string Type, string Payload, int Attempts);
}
