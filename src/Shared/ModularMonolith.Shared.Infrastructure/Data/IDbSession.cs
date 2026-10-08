using Npgsql;

namespace ModularMonolith.Shared.Infrastructure.Data;

/// <summary>
/// Scoped database session: one connection per DI scope, already switched to the application role
/// and bound to the current tenant (<c>app.tenant_id</c>) so PostgreSQL RLS filters every statement.
/// </summary>
public interface IDbSession
{
    /// <summary>Gets the active transaction, or <see langword="null"/> outside a unit of work.</summary>
    NpgsqlTransaction? Transaction { get; }

    /// <summary>Gets (and lazily opens) the session connection.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The open connection.</returns>
    Task<NpgsqlConnection> GetConnectionAsync(CancellationToken cancellationToken);
}
