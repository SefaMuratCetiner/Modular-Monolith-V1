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
    /// Gets or sets the non-superuser role every application connection switches to (<c>SET ROLE</c>).
    /// Row-level security never applies to superusers, so this guarantees tenant isolation even when
    /// the login used in development is a superuser. Leave empty only if the login itself is a
    /// non-superuser that owns no tables.
    /// </summary>
    public string ApplicationRole { get; set; } = "modular_monolith_app";

    /// <summary>Gets or sets a value indicating whether migrations run at startup.</summary>
    public bool MigrateOnStartup { get; set; } = true;
}
