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
/// Applies each module's embedded SQL migrations (DbUp) into the module's own schema.
/// A PostgreSQL advisory lock serializes concurrent replicas, so rolling deployments are safe.
/// </summary>
public sealed class ModuleMigrator(
    ModuleCatalog catalog,
    IConfiguration configuration,
    IOptions<DatabaseOptions> options,
    ILogger<ModuleMigrator> logger)
{
    // Arbitrary but stable application-wide lock id.
    private const long AdvisoryLockKey = 7_420_310_001;

    /// <summary>Runs all pending migrations for all modules.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    public async Task MigrateAsync(CancellationToken cancellationToken)
    {
        var connectionString = configuration.GetConnectionString(options.Value.ConnectionStringName)
            ?? throw new InvalidOperationException($"Connection string '{options.Value.ConnectionStringName}' is not configured.");

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition("select pg_advisory_lock(@key)", new { key = AdvisoryLockKey }, cancellationToken: cancellationToken));
        try
        {
            var role = options.Value.ApplicationRole;
            if (!string.IsNullOrWhiteSpace(role))
            {
                await EnsureApplicationRoleAsync(connection, role, cancellationToken);
            }

            foreach (var module in catalog.Modules)
            {
                await MigrateModuleAsync(connection, connectionString, module, role, cancellationToken);
            }
        }
        finally
        {
            await connection.ExecuteAsync(new CommandDefinition("select pg_advisory_unlock(@key)", new { key = AdvisoryLockKey }, cancellationToken: CancellationToken.None));
        }
    }

    private static async Task EnsureApplicationRoleAsync(NpgsqlConnection connection, string role, CancellationToken cancellationToken)
    {
        var quoted = SqlIdentifier.Quote(role);

        // NOLOGIN role: application logins switch to it with SET ROLE. It is never a superuser,
        // never owns tables and therefore always subject to row-level security.
        var sql = $"""
            do $$
            begin
                if not exists (select 1 from pg_roles where rolname = '{role}') then
                    create role {quoted} nologin;
                end if;
            end
            $$;
            grant {quoted} to current_user;
            """;
        await connection.ExecuteAsync(new CommandDefinition(sql, cancellationToken: cancellationToken));
    }

    private async Task MigrateModuleAsync(NpgsqlConnection connection, string connectionString, IModule module, string role, CancellationToken cancellationToken)
    {
        var schema = SqlIdentifier.Quote(module.Schema);

        // The journal table lives in the module schema, so the schema must exist first.
        await connection.ExecuteAsync(new CommandDefinition($"create schema if not exists {schema};", cancellationToken: cancellationToken));

        var result = DeployChanges.To
            .PostgresqlDatabase(connectionString)
            .WithScriptsEmbeddedInAssembly(
                module.Assembly,
                name => name.Contains(".Migrations.", StringComparison.Ordinal) && name.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
            .JournalToPostgresqlTable(module.Schema, "schema_versions")
            .WithVariablesDisabled() // scripts use $$ dollar quoting
            .WithTransactionPerScript()
            .LogToNowhere()
            .Build()
            .PerformUpgrade();

        if (!result.Successful)
        {
            throw new InvalidOperationException($"Migrations failed for module {module.Name}.", result.Error);
        }

        var appliedScripts = result.Scripts.Count();
        logger.MigrationsApplied(appliedScripts, module.Name);

        if (!string.IsNullOrWhiteSpace(role))
        {
            var quotedRole = SqlIdentifier.Quote(role);
            var grants = $"""
                grant usage on schema {schema} to {quotedRole};
                grant select, insert, update, delete on all tables in schema {schema} to {quotedRole};
                grant usage, select on all sequences in schema {schema} to {quotedRole};
                """;
            await connection.ExecuteAsync(new CommandDefinition(grants, cancellationToken: cancellationToken));
        }
    }
}
