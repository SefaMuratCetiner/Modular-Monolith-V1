namespace ModularMonolith.Shared.Kernel.Domain;

/// <summary>
/// Consistency boundary that raises domain events.
/// </summary>
/// <typeparam name="TId">The strongly typed identifier.</typeparam>
public abstract class AggregateRoot<TId> : Entity<TId>, IHasDomainEvents
    where TId : struct, IEquatable<TId>
{
    private readonly List<IDomainEvent> _domainEvents = [];

    /// <summary>Initializes a new instance of the <see cref="AggregateRoot{TId}"/> class.</summary>
    /// <param name="id">The aggregate identifier.</param>
    protected AggregateRoot(TId id)
        : base(id)
    {
    }

    /// <inheritdoc />
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    /// <inheritdoc />
    public void ClearDomainEvents() => _domainEvents.Clear();

    /// <summary>Records a domain event to be dispatched inside the current transaction.</summary>
    /// <param name="domainEvent">The event.</param>
    protected void Raise(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);
}
