namespace ModularMonolith.Shared.Kernel.Domain;

/// <summary>
/// Consistency boundary that raises domain events.
/// </summary>
public abstract class AggregateRoot : Entity, IHasDomainEvents
{
    private readonly List<IDomainEvent> _domainEvents = [];

    /// <summary>Initializes a new instance of the <see cref="AggregateRoot"/> class.</summary>
    /// <param name="id">The aggregate identifier.</param>
    protected AggregateRoot(Guid id)
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
