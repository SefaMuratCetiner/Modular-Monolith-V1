using ModularMonolith.Shared.Kernel.Domain;

namespace ModularMonolith.Shared.Infrastructure.Data;

/// <summary>
/// Transaction boundary for a command or an integration-event handler. Owned by the pipeline,
/// never by repositories, so one command can change several aggregates atomically.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>Gets a value indicating whether a transaction is active.</summary>
    bool IsActive { get; }

    /// <summary>Registers an aggregate whose domain events must be dispatched before commit.</summary>
    /// <param name="aggregate">The aggregate.</param>
    void Track(IHasDomainEvents aggregate);

    /// <summary>Records a before/after snapshot that is written to the audit log with the command.</summary>
    /// <param name="change">The change.</param>
    void RecordChange(AuditChange change);

    /// <summary>Registers a callback that runs only after a successful commit (e.g. cache invalidation).</summary>
    /// <param name="callback">The callback.</param>
    void OnCommitted(Func<CancellationToken, Task> callback);
}
