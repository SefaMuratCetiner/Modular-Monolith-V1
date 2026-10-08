using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModularMonolith.Shared.Infrastructure.Observability;
using ModularMonolith.Shared.Kernel.Context;
using ModularMonolith.Shared.Kernel.Domain;
using Npgsql;

namespace ModularMonolith.Shared.Infrastructure.Data;

/// <summary>
/// Default <see cref="IDbSession"/> / <see cref="IUnitOfWork"/> implementation (scoped).
/// </summary>
internal sealed class DbSession(
    NpgsqlDataSource dataSource,
    ITenantContext tenantContext,
    IOptions<DatabaseOptions> options,
    ILogger<DbSession> logger) : IDbSession, IUnitOfWork, IAsyncDisposable
{
    private readonly List<IHasDomainEvents> _trackedAggregates = [];
    private readonly List<Func<CancellationToken, Task>> _afterCommit = [];
    private readonly List<AuditChange> _changes = [];
    private NpgsqlConnection? _connection;

    /// <inheritdoc />
    public NpgsqlTransaction? Transaction { get; private set; }

    /// <inheritdoc />
    public bool IsActive => Transaction is not null;

    /// <summary>
    /// Gets a value indicating whether COMMIT was sent for the current unit of work. After that point
    /// a failure has an unknown outcome and must not be retried.
    /// </summary>
    public bool CommitStarted { get; private set; }

    /// <summary>Gets the aggregates tracked in the current unit of work.</summary>
    public IReadOnlyList<IHasDomainEvents> TrackedAggregates => _trackedAggregates;

    /// <summary>Gets the before/after snapshots recorded in the current unit of work.</summary>
    public IReadOnlyList<AuditChange> Changes => _changes;

    /// <inheritdoc />
    public async Task<NpgsqlConnection> GetConnectionAsync(CancellationToken cancellationToken)
    {
        if (_connection is not null)
        {
            return _connection;
        }

        var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            var role = options.Value.ApplicationRole;

            // 1) Restrict the session to the application role (DML only, RLS enforced). 2) Bind the tenant for RLS.
            //    An unresolved tenant binds '' which matches no rows and fails CHECK constraints (fail closed).
            command.CommandText = $"set role {SqlIdentifier.Quote(role)}; select set_config('app.tenant_id', @tenant, false)";
            command.Parameters.AddWithValue("tenant", tenantContext.IsResolved ? tenantContext.TenantId : string.Empty);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }

        _connection = connection;
        return connection;
    }

    /// <inheritdoc />
    public void Track(IHasDomainEvents aggregate)
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        if (!_trackedAggregates.Contains(aggregate))
        {
            _trackedAggregates.Add(aggregate);
        }
    }

    /// <inheritdoc />
    public void RecordChange(AuditChange change)
    {
        ArgumentNullException.ThrowIfNull(change);
        _changes.Add(change);
    }

    /// <inheritdoc />
    public void OnCommitted(Func<CancellationToken, Task> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        _afterCommit.Add(callback);
    }

    /// <summary>Begins a transaction on the session connection.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    public async Task BeginAsync(CancellationToken cancellationToken)
    {
        if (Transaction is not null)
        {
            throw new InvalidOperationException("A unit of work is already active in this scope.");
        }

        var connection = await GetConnectionAsync(cancellationToken);
        Transaction = await connection.BeginTransactionAsync(cancellationToken);
        CommitStarted = false;
    }

    /// <summary>Commits the transaction and runs after-commit callbacks.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    public async Task CommitAsync(CancellationToken cancellationToken)
    {
        var transaction = Transaction ?? throw new InvalidOperationException("No active unit of work.");
        CommitStarted = true;
        await transaction.CommitAsync(cancellationToken);
        await ResetTransactionAsync();

        Func<CancellationToken, Task>[] callbacks = [.. _afterCommit];
        _afterCommit.Clear();
        foreach (var callback in callbacks)
        {
            try
            {
                await callback(cancellationToken);
            }
#pragma warning disable CA1031 // The data is already committed; a side effect (cache, job trigger) must not fail the request.
            catch (Exception exception)
#pragma warning restore CA1031
            {
                logger.AfterCommitFailed(exception);
            }
        }
    }

    /// <summary>Rolls back the transaction (no-op when none is active).</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    public async Task RollbackAsync(CancellationToken cancellationToken)
    {
        if (Transaction is null)
        {
            return;
        }

        try
        {
            await Transaction.RollbackAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is NpgsqlException or InvalidOperationException)
        {
            // The connection broke (that is often why we are rolling back): the server has already
            // discarded the transaction, so drop the connection and let the next use open a new one.
            await DiscardConnectionAsync();
        }
        finally
        {
            await ResetTransactionAsync();
            _afterCommit.Clear();
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (Transaction is not null)
        {
            await Transaction.DisposeAsync();
        }

        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }
    }

    /// <summary>
    /// Disposes the current connection (and transaction) so the next operation opens a fresh one.
    /// Used after transient failures, when the old connection may be unusable.
    /// </summary>
    /// <returns>A task.</returns>
    public async Task DiscardConnectionAsync()
    {
        if (Transaction is not null)
        {
            await Transaction.DisposeAsync();
            Transaction = null;
        }

        if (_connection is not null)
        {
            await _connection.DisposeAsync();
            _connection = null;
        }
    }

    private async Task ResetTransactionAsync()
    {
        if (Transaction is not null)
        {
            await Transaction.DisposeAsync();
            Transaction = null;
        }

        _trackedAggregates.Clear();
        _changes.Clear();
    }
}
