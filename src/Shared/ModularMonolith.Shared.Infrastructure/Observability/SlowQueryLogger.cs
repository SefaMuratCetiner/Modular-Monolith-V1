using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModularMonolith.Shared.Infrastructure.Data;

namespace ModularMonolith.Shared.Infrastructure.Observability;

/// <summary>
/// Logs every database command slower than <see cref="DatabaseOptions.SlowQueryThresholdMs"/> with its
/// SQL text (parameter placeholders only, never values), duration and trace id, and counts them in
/// <c>modmono.db.slow_queries</c>. It listens to Npgsql's built-in activities, so it covers Dapper,
/// the outbox, Hangfire's storage and any other Npgsql usage without wrapping commands.
/// </summary>
internal sealed class SlowQueryLogger(IOptions<DatabaseOptions> options, ILogger<SlowQueryLogger> logger) : IHostedService, IDisposable
{
    private const string NpgsqlSource = "Npgsql";
    private const int MaxStatementLength = 2000;

    private ActivityListener? _listener;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var thresholdMs = options.Value.SlowQueryThresholdMs;
        if (thresholdMs <= 0)
        {
            return Task.CompletedTask;
        }

        var threshold = TimeSpan.FromMilliseconds(thresholdMs);
        _listener = new ActivityListener
        {
            ShouldListenTo = source => string.Equals(source.Name, NpgsqlSource, StringComparison.Ordinal),

            // AllData (not "recorded"): Npgsql fills the SQL tags without forcing spans into exported traces.
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activity =>
            {
                if (activity.Duration < threshold)
                {
                    return;
                }

                var statement = activity.GetTagItem("db.query.text") as string
                    ?? activity.GetTagItem("db.statement") as string
                    ?? activity.DisplayName;
                if (statement.Length > MaxStatementLength)
                {
                    statement = statement[..MaxStatementLength];
                }

                Telemetry.SlowQueries.Add(1);
                logger.SlowQuery(activity.Duration.TotalMilliseconds, statement, activity.TraceId.ToString());
            },
        };
        ActivitySource.AddActivityListener(_listener);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public void Dispose() => _listener?.Dispose();
}
