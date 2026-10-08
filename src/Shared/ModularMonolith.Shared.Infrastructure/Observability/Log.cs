using Microsoft.Extensions.Logging;

namespace ModularMonolith.Shared.Infrastructure.Observability;

/// <summary>
/// Source-generated, allocation-free log messages for the building blocks.
/// </summary>
internal static partial class Log
{
    [LoggerMessage(EventId = 1000, Level = LogLevel.Debug, Message = "Handling {RequestKind} {RequestName}")]
    public static partial void Handling(this ILogger logger, string requestKind, string requestName);

    [LoggerMessage(EventId = 1001, Level = LogLevel.Information, Message = "{RequestKind} {RequestName} failed with {ErrorCode}")]
    public static partial void HandlerFailed(this ILogger logger, string requestKind, string requestName, string errorCode);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Debug, Message = "Handled {RequestKind} {RequestName}")]
    public static partial void Handled(this ILogger logger, string requestKind, string requestName);

    [LoggerMessage(EventId = 1100, Level = LogLevel.Warning, Message = "Outbox message {MessageId} failed (attempt {Attempt}); next attempt at {NextAttemptAt}")]
    public static partial void OutboxRetry(this ILogger logger, Exception exception, Guid messageId, int attempt, DateTimeOffset nextAttemptAt);

    [LoggerMessage(EventId = 1101, Level = LogLevel.Error, Message = "Outbox message {MessageId} dead-lettered after {Attempt} attempts")]
    public static partial void OutboxDeadLettered(this ILogger logger, Exception exception, Guid messageId, int attempt);

    [LoggerMessage(EventId = 1102, Level = LogLevel.Warning, Message = "Could not trigger immediate outbox dispatch for schema {Schema}; the recurring job will pick it up")]
    public static partial void OutboxTriggerFailed(this ILogger logger, Exception exception, string schema);

    [LoggerMessage(EventId = 1200, Level = LogLevel.Warning, Message = "Handler {Handler} failed for {EventType} (attempt {Attempt})")]
    public static partial void EventHandlerRetry(this ILogger logger, Exception exception, string handler, string eventType, int attempt);

    [LoggerMessage(EventId = 1300, Level = LogLevel.Information, Message = "Applied {Count} migration script(s) for module {Module}")]
    public static partial void MigrationsApplied(this ILogger logger, int count, string module);

    [LoggerMessage(EventId = 1400, Level = LogLevel.Warning, Message = "After-commit callback failed")]
    public static partial void AfterCommitFailed(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 1500, Level = LogLevel.Warning, Message = "Cache unavailable for key {Key}; falling back to the source")]
    public static partial void CacheUnavailable(this ILogger logger, Exception exception, string key);

    [LoggerMessage(EventId = 1501, Level = LogLevel.Warning, Message = "Idempotency store unavailable; processing request without idempotency protection")]
    public static partial void IdempotencyUnavailable(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 1600, Level = LogLevel.Error, Message = "Unhandled exception while processing {Method} {Path}")]
    public static partial void UnhandledException(this ILogger logger, Exception exception, string method, string path);
}
