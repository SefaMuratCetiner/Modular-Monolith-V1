using System.Reflection;
using Dapper;
using DbUp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModularMonolith.Shared.Infrastructure.Data;
using ModularMonolith.Shared.Infrastructure.Modules;
using ModularMonolith.Shared.Infrastructure.Observability;
using Npgsql;

namespace ModularMonolith.Shared.Infrastructure.Migrations;

/// <summary>
/// Database setup, run by the separate <c>migrate</c> command (never by the serving application):
/// <list type="number">
/// <item>Takes a PostgreSQL advisory lock, so concurrent runs are serialized.</item>
/// <item>Ensures the <c>NOLOGIN</c> application role and, optionally, the application login (member of the role).</item>
/// <item>Applies the platform scripts (schema <c>platform</c>) and every module's scripts (its own schema) with DbUp.
/// Scripts grant the application role exactly the privileges each table needs (<c>$app_role$</c> variable).</item>
/// <item>Runs extra <see cref="IDatabaseSetupStep"/>s (e.g. the Hangfire job store).</item>
/// </list>
/// It connects as the schema owner (<see cref="MigrationOptions.ConnectionStringName"/>); the application
/// connects with a login that owns nothing and has only the granted DML privileges.
/// </summary>
public sealed class ModuleMigrator(
    ModuleCatalog catalog,
    IConfiguration configuration,
    IOptions<DatabaseOptions> database,
    IOptions<MigrationOptions> migration,
    IEnumerable<IDatabaseSetupStep> setupSteps,
    ILogger<ModuleMigrator> logger)
{
    /// <summary>The schema of platform-level tables (e.g. Data Protection keys).</summary>
    public const string PlatformSchema = "platform";

    // Arbitrary but stable application-wide lock id.
    private const long AdvisoryLockKey = 7_420_310_001;

    /// <summary>Runs all pending migrations and setup steps.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    public async Task MigrateAsync(CancellationToken cancellationToken)
    {
        var settings = migration.Value;
        var connectionString = configuration.GetConnectionString(settings.ConnectionStringName)
            ?? throw new InvalidOperationException($"Connection string '{settings.ConnectionStringName}' is not configured.");
        var role = SqlIdentifier.Quote(database.Value.ApplicationRole);

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition("select pg_advisory_lock(@key)", new { key = AdvisoryLockKey }, cancellationToken: cancellationToken));
        try
        {
            await EnsureApplicationRoleAsync(connection, database.Value.ApplicationRole, cancellationToken);
            await EnsureApplicationLoginAsync(connection, settings, role, cancellationToken);

            await MigrateSchemaAsync(connection, connectionString, "Platform", PlatformSchema, typeof(ModuleMigrator).Assembly, role, cancellationToken);
            foreach (var module in catalog.Modules)
            {
                await MigrateSchemaAsync(connection, connectionString, module.Name, module.Schema, module.Assembly, role, cancellationToken);
            }

            foreach (var step in setupSteps)
            {
                await step.ExecuteAsync(connection, role, cancellationToken);
                logger.SetupStepCompleted(step.Name);
            }
        }
        finally
        {
            await connection.ExecuteAsync(new CommandDefinition("select pg_advisory_unlock(@key)", new { key = AdvisoryLockKey }, cancellationToken: CancellationToken.None));
        }
    }

    private static async Task EnsureApplicationRoleAsync(NpgsqlConnection connection, string role, CancellationToken cancellationToken)
    {
        // NOLOGIN, not a superuser, owns nothing: always subject to row-level security.
        var exists = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "select exists (select 1 from pg_roles where rolname = @Role)",
            new { Role = role },
            cancellationToken: cancellationToken));
        if (!exists)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                $"create role {SqlIdentifier.Quote(role)} nologin nosuperuser nocreatedb nocreaterole",
                cancellationToken: cancellationToken));
        }
    }

    private static async Task EnsureApplicationLoginAsync(NpgsqlConnection connection, MigrationOptions settings, string quotedRole, CancellationToken cancellationToken)
    {
        if (settings.ApplicationLogin is not { } login)
        {
            return;
        }

        if (!string.IsNullOrEmpty(settings.ApplicationLoginPassword))
        {
            // format(%I, %L) quotes the identifier and the password literal server-side (no SQL injection).
            var statement = await connection.ExecuteScalarAsync<string>(new CommandDefinition(
                """
                select case when exists (select 1 from pg_roles where rolname = @Login)
                    then format('alter role %I with login nosuperuser nocreatedb nocreaterole inherit password %L', @Login, @Password)
                    else format('create role %I with login nosuperuser nocreatedb nocreaterole inherit password %L', @Login, @Password)
                end
                """,
                new { Login = login, Password = settings.ApplicationLoginPassword },
                cancellationToken: cancellationToken));
            await connection.ExecuteAsync(new CommandDefinition(statement!, cancellationToken: cancellationToken));
        }

        // The login gets its privileges only through the application role.
        await connection.ExecuteAsync(new CommandDefinition(
            $"grant {quotedRole} to {SqlIdentifier.Quote(login)}",
            cancellationToken: cancellationToken));
    }

    private async Task MigrateSchemaAsync(
        NpgsqlConnection connection,
        string connectionString,
        string name,
        string schema,
        Assembly assembly,
        string quotedRole,
        CancellationToken cancellationToken)
    {
        // The journal table lives in the target schema, so the schema must exist first.
        await connection.ExecuteAsync(new CommandDefinition($"create schema if not exists {SqlIdentifier.Quote(schema)};", cancellationToken: cancellationToken));

        var result = DeployChanges.To
            .PostgresqlDatabase(connectionString)
            .WithScriptsEmbeddedInAssembly(
                assembly,
                resource => resource.Contains(".Migrations.", StringComparison.Ordinal) && resource.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
            .JournalToPostgresqlTable(schema, "schema_versions")
            .WithVariable("app_role", quotedRole) // scripts write: grant ... to $app_role$;
            .WithTransactionPerScript()
            .LogToNowhere()
            .Build()
            .PerformUpgrade();

        if (!result.Successful)
        {
            throw new InvalidOperationException($"Migrations failed for {name}.", result.Error);
        }

        var appliedScripts = result.Scripts.Count();
        logger.MigrationsApplied(appliedScripts, name);
    }
}
