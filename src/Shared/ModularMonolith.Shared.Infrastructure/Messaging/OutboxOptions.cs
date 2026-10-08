namespace ModularMonolith.Shared.Infrastructure.Messaging;

/// <summary>
/// Outbox processing settings (section <c>Outbox</c>).
/// </summary>
public sealed class OutboxOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "Outbox";

    /// <summary>Gets or sets the number of messages claimed per batch.</summary>
    public int BatchSize { get; set; } = 50;

    /// <summary>Gets or sets the maximum batches per job run (bounds a single run's duration).</summary>
    public int MaxBatchesPerRun { get; set; } = 20;

    /// <summary>Gets or sets the attempts after which a message is dead-lettered.</summary>
    public int MaxAttempts { get; set; } = 8;

    /// <summary>Gets or sets the base retry delay in seconds (exponential backoff).</summary>
    public int BaseDelaySeconds { get; set; } = 5;

    /// <summary>Gets or sets the maximum retry delay in seconds.</summary>
    public int MaxDelaySeconds { get; set; } = 3600;

    /// <summary>Gets or sets the recurring sweep cron expression (safety net for missed triggers).</summary>
    public string SweepCron { get; set; } = "* * * * *";
}
