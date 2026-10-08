using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using Dapper;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModularMonolith.Shared.Infrastructure.Data;
using ModularMonolith.Shared.Infrastructure.Modules;
using ModularMonolith.Shared.Infrastructure.Observability;
using Npgsql;

namespace ModularMonolith.Shared.Infrastructure.Messaging;

/// <summary>
/// Samples every module's outbox periodically and exposes the backlog as gauges:
/// <c>modmono.outbox.pending</c>, <c>modmono.outbox.dead</c> and <c>modmono.outbox.oldest_pending_age</c>
/// (seconds). Sampling in the background keeps metric scrapes free of database queries.
/// </summary>
internal sealed class OutboxMetricsCollector : BackgroundService
{
    private readonly ConcurrentDictionary<string, OutboxBacklog> _snapshot = new(StringComparer.Ordinal);
    private readonly NpgsqlDataSource _dataSource;
    private readonly ModuleCatalog _modules;
    private readonly IOptions<OutboxOptions> _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<OutboxMetricsCollector> _logger;

    public OutboxMetricsCollector(
        NpgsqlDataSource dataSource,
        ModuleCatalog modules,
        IOptions<OutboxOptions> options,
        TimeProvider timeProvider,
        ILogger<OutboxMetricsCollector> logger)
    {
        _dataSource = dataSource;
        _modules = modules;
        _options = options;
        _timeProvider = timeProvider;
        _logger = logger;

        Telemetry.Meter.CreateObservableGauge(
            "modmono.outbox.pending",
            () => Measure(backlog => backlog.Pending),
            description: "Outbox messages waiting for delivery (pending or leased).");
        Telemetry.Meter.CreateObservableGauge(
            "modmono.outbox.dead",
            () => Measure(backlog => backlog.Dead),
            description: "Dead-lettered outbox messages awaiting inspection or replay.");
        Telemetry.Meter.CreateObservableGauge(
            "modmono.outbox.oldest_pending_age",
            () => _snapshot.Select(entry => new Measurement<double>(entry.Value.OldestPendingSeconds, new KeyValuePair<string, object?>("module", entry.Key))),
            unit: "s",
            description: "Age of the oldest undelivered outbox message (consumer lag).");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_options.Value.MetricsIntervalSeconds), _timeProvider);
        do
        {
            try
            {
                await CollectAsync(stoppingToken);
            }
            catch (Exception exception) when (exception is NpgsqlException or TimeoutException)
            {
                _logger.OutboxMetricsFailed(exception);
            }
        }
        while (await WaitForNextTickAsync(timer, stoppingToken));
    }

    private static async Task<bool> WaitForNextTickAsync(PeriodicTimer timer, CancellationToken stoppingToken)
    {
        try
        {
            return await timer.WaitForNextTickAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private async Task CollectAsync(CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        foreach (var module in _modules.Modules)
        {
            var table = $"{SqlIdentifier.Quote(module.Schema)}.outbox_messages";
            var backlog = await connection.QuerySingleAsync<OutboxBacklog>(new CommandDefinition(
                $"""
                select count(*) filter (where status in ('pending', 'processing')) as Pending,
                       count(*) filter (where status = 'dead') as Dead,
                       coalesce(extract(epoch from (now() - min(occurred_at) filter (where status in ('pending', 'processing')))), 0)::float8 as OldestPendingSeconds
                from {table}
                """,
                cancellationToken: cancellationToken));
            _snapshot[module.Schema] = backlog;
        }
    }

    private IEnumerable<Measurement<long>> Measure(Func<OutboxBacklog, long> selector)
        => _snapshot.Select(entry => new Measurement<long>(selector(entry.Value), new KeyValuePair<string, object?>("module", entry.Key)));

    private sealed class OutboxBacklog
    {
        public long Pending { get; init; }

        public long Dead { get; init; }

        public double OldestPendingSeconds { get; init; }
    }
}
