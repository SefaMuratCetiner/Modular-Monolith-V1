namespace ModularMonolith.Host;

/// <summary>Source-generated log messages of the host.</summary>
internal static partial class HostLog
{
    [LoggerMessage(EventId = 2000, Level = LogLevel.Information, Message = "Database migrations completed")]
    public static partial void MigrationsCompleted(ILogger logger);

    [LoggerMessage(EventId = 2001, Level = LogLevel.Critical, Message = "Database migrations failed")]
    public static partial void MigrationsFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 2010, Level = LogLevel.Information, Message = "OpenAPI contract written to {Path}")]
    public static partial void OpenApiWritten(ILogger logger, string path);

    [LoggerMessage(EventId = 2011, Level = LogLevel.Information, Message = "OpenAPI contract {Path} is up to date")]
    public static partial void OpenApiUpToDate(ILogger logger, string path);

    [LoggerMessage(EventId = 2012, Level = LogLevel.Error, Message = "OpenAPI contract {Path} is out of date: run the '{Command}' command and commit the result")]
    public static partial void OpenApiOutdated(ILogger logger, string path, string command);

    [LoggerMessage(EventId = 2020, Level = LogLevel.Information, Message = "Personal data of subject {SubjectId} exported ({SectionCount} module sections)")]
    public static partial void PersonalDataExported(ILogger logger, string subjectId, int sectionCount);

    [LoggerMessage(EventId = 2100, Level = LogLevel.Information, Message = "Shutdown requested: readiness is now unhealthy; draining for {DrainSeconds}s before closing connections")]
    public static partial void DrainStarted(ILogger logger, int drainSeconds);
}
