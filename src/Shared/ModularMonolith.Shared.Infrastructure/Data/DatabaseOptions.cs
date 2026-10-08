namespace ModularMonolith.Shared.Infrastructure.Data;

/// <summary>
/// Database settings (section <c>Database</c>).
/// </summary>
public sealed class DatabaseOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "Database";

    /// <summary>Gets or sets the connection string name (<c>ConnectionStrings:{name}</c>).</summary>
    public string ConnectionStringName { get; set; } = "postgres";

    /// <summary>
    /// Gets or sets the connection string. Resolved once at startup from
    /// <c>ConnectionStrings:{ConnectionStringName}</c> when not set explicitly, so no component has to
    /// read <c>IConfiguration</c> at runtime.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Gets or sets the <c>NOLOGIN</c> role that holds the application's table privileges. The application
    /// login is a member of it and every session additionally switches to it (<c>SET ROLE</c>), so the
    /// connection never has more than these privileges and row-level security always applies.
    /// </summary>
    public string ApplicationRole { get; set; } = "modular_monolith_app";

    /// <summary>
    /// Gets or sets how many times a command is re-executed after a transient database failure
    /// (connection loss, deadlock, serialization failure) that happened before commit.
    /// </summary>
    public int TransientRetries { get; set; } = 2;

    /// <summary>
    /// Gets or sets the duration above which a database command is logged as slow (0 disables).
    /// </summary>
    public int SlowQueryThresholdMs { get; set; } = 500;
}
