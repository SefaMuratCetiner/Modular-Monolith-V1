using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace ModularMonolith.Shared.Infrastructure.Observability;

/// <summary>
/// Application ActivitySource and Meter. Names start with <c>ModularMonolith</c> so that
/// ServiceDefaults can subscribe with a <c>ModularMonolith*</c> wildcard.
/// </summary>
public static class Telemetry
{
    /// <summary>The ActivitySource / Meter name.</summary>
    public const string Name = "ModularMonolith.Application";

    /// <summary>Gets the activity source used for commands, queries and messaging.</summary>
    public static ActivitySource ActivitySource { get; } = new(Name);

    /// <summary>Gets the application meter.</summary>
    public static Meter Meter { get; } = new(Name);

    /// <summary>Gets the command/query duration histogram (milliseconds).</summary>
    public static Histogram<double> RequestDuration { get; } =
        Meter.CreateHistogram<double>("modmono.handler.duration", unit: "ms", description: "Command and query handler duration.");

    /// <summary>Gets the counter of successfully dispatched outbox messages.</summary>
    public static Counter<long> OutboxProcessed { get; } =
        Meter.CreateCounter<long>("modmono.outbox.processed", description: "Outbox messages dispatched successfully.");

    /// <summary>Gets the counter of failed outbox dispatch attempts.</summary>
    public static Counter<long> OutboxFailed { get; } =
        Meter.CreateCounter<long>("modmono.outbox.failed", description: "Outbox dispatch attempts that failed and will be retried.");

    /// <summary>Gets the counter of database commands slower than the configured threshold.</summary>
    public static Counter<long> SlowQueries { get; } =
        Meter.CreateCounter<long>("modmono.db.slow_queries", description: "Database commands slower than Database:SlowQueryThresholdMs.");

    /// <summary>Gets the counter of dead-lettered outbox messages.</summary>
    public static Counter<long> OutboxDeadLettered { get; } =
        Meter.CreateCounter<long>("modmono.outbox.dead_lettered", description: "Outbox messages that exhausted all retries.");
}
