namespace ModularMonolith.Host;

/// <summary>Source-generated log messages of the host.</summary>
internal static partial class HostLog
{
    [LoggerMessage(EventId = 2000, Level = LogLevel.Information, Message = "Database migrations completed")]
    public static partial void MigrationsCompleted(ILogger logger);

    [LoggerMessage(EventId = 2001, Level = LogLevel.Critical, Message = "Database migrations failed")]
    public static partial void MigrationsFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 2100, Level = LogLevel.Information, Message = "Shutdown requested: readiness is now unhealthy; draining for {DrainSeconds}s before closing connections")]
    public static partial void DrainStarted(ILogger logger, int drainSeconds);
}
