using Dapper;
using Hangfire.PostgreSql;
using ModularMonolith.Shared.Infrastructure.Migrations;
using Npgsql;

namespace ModularMonolith.Host.Migrations;

/// <summary>
/// Installs/upgrades the Hangfire job-store schema with the owner's rights (the application runs
/// Hangfire with <c>PrepareSchemaIfNecessary = false</c>) and grants the application role DML on it.
/// </summary>
internal sealed class HangfireSchemaStep : IDatabaseSetupStep
{
    public const string Schema = "hangfire";

    public string Name => "hangfire-job-store";

    public async Task ExecuteAsync(NpgsqlConnection ownerConnection, string quotedApplicationRole, CancellationToken cancellationToken)
    {
        PostgreSqlObjectsInstaller.Install(ownerConnection, Schema);

        // Hangfire's tables are an internal job store: DML on all of them, no DDL.
        await ownerConnection.ExecuteAsync(new CommandDefinition(
            $"""
            grant usage on schema {Schema} to {quotedApplicationRole};
            grant select, insert, update, delete on all tables in schema {Schema} to {quotedApplicationRole};
            grant usage, select, update on all sequences in schema {Schema} to {quotedApplicationRole};
            """,
            cancellationToken: cancellationToken));
    }
}
