namespace ModularMonolith.Shared.Infrastructure.Messaging;

/// <summary>
/// Data retention for the per-module system tables (section <c>Retention</c>). A value of 0 keeps rows forever.
/// </summary>
public sealed class RetentionOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "Retention";

    /// <summary>Gets or sets how long delivered outbox messages are kept.</summary>
    public int ProcessedOutboxDays { get; set; } = 7;

    /// <summary>
    /// Gets or sets how long dead-lettered outbox messages are kept for inspection and replay (counted from
    /// when the event occurred). Dead letters can contain personal data, so they must not live forever.
    /// </summary>
    public int DeadLetterDays { get; set; } = 30;

    /// <summary>
    /// Gets or sets how long audit rows are kept. Set it to your legal retention requirement (minimum 30;
    /// the database function refuses anything shorter). 0 keeps them forever.
    /// </summary>
    public int AuditLogDays { get; set; } = 365;

    /// <summary>
    /// Gets or sets after how many days the client IP address is removed from audit rows (the rest of the row
    /// stays until <see cref="AuditLogDays"/>). IP addresses are personal data and only needed for recent
    /// security investigations.
    /// </summary>
    public int AuditIpAddressDays { get; set; } = 90;

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
