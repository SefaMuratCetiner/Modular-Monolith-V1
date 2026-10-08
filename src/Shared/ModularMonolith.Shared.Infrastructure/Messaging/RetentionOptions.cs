namespace ModularMonolith.Shared.Infrastructure.Messaging;

/// <summary>
/// Data retention for the per-module system tables (section <c>Retention</c>). A value of 0 keeps rows forever.
/// </summary>
public sealed class RetentionOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "Retention";

    /// <summary>Gets or sets how long delivered outbox messages are kept (dead letters are never purged).</summary>
    public int ProcessedOutboxDays { get; set; } = 7;

    /// <summary>
    /// Gets or sets how long inbox records are kept. Must exceed the longest possible redelivery window
    /// (outbox retries plus dead-letter replay), otherwise a late redelivery would be processed twice.
    /// </summary>
    public int InboxDays { get; set; } = 30;

    /// <summary>Gets or sets the rows deleted per statement (keeps locks and WAL bursts small).</summary>
    public int BatchSize { get; set; } = 5000;

    /// <summary>Gets or sets the cron expression of the daily retention job.</summary>
    public string Cron { get; set; } = "17 3 * * *";
}
