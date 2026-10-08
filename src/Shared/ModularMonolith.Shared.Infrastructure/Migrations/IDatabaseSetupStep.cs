using Npgsql;

namespace ModularMonolith.Shared.Infrastructure.Migrations;

/// <summary>
/// An extra setup step run by the migrator with the schema owner's connection, after the module
/// migrations (e.g. installing the Hangfire job-store schema and granting the application role access).
/// </summary>
public interface IDatabaseSetupStep
{
    /// <summary>Gets a name for logs.</summary>
    string Name { get; }

    /// <summary>Executes the step. Must be idempotent: it runs on every deployment.</summary>
    /// <param name="ownerConnection">An open connection of the schema owner.</param>
    /// <param name="quotedApplicationRole">The application role, already validated and quoted.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    Task ExecuteAsync(NpgsqlConnection ownerConnection, string quotedApplicationRole, CancellationToken cancellationToken);
}
