namespace ModularMonolith.Shared.Infrastructure.Migrations;

/// <summary>
/// Settings of the <c>migrate</c> command (section <c>Migrations</c>). Only the migration process uses
/// them; the running application never holds the schema owner's credentials.
/// </summary>
public sealed class MigrationOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "Migrations";

    /// <summary>
    /// Gets or sets the connection string name of the schema owner (<c>ConnectionStrings:{name}</c>).
    /// This login owns the schemas and runs DDL; it is never given to the application.
    /// </summary>
    public string ConnectionStringName { get; set; } = "postgres-migrations";

    /// <summary>
    /// Gets or sets the login the application connects with (e.g. <c>modular_monolith_api</c>).
    /// When set, the migrator grants it membership of <c>Database:ApplicationRole</c>.
    /// </summary>
    public string? ApplicationLogin { get; set; }

    /// <summary>
    /// Gets or sets the password of <see cref="ApplicationLogin"/>. Development convenience only: when set,
    /// the migrator creates the login (or resets its password). In production leave it empty and let
    /// your DBA / secret store provision the login.
    /// </summary>
    public string? ApplicationLoginPassword { get; set; }
}
