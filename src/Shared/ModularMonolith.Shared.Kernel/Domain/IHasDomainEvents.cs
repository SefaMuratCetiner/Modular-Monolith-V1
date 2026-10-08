namespace ModularMonolith.Shared.Kernel.Domain;

/// <summary>
/// Exposes pending domain events so the unit of work can dispatch them before commit.
/// </summary>
public interface IHasDomainEvents
{
    /// <summary>Gets the domain events raised since the aggregate was loaded or created.</summary>
    IReadOnlyCollection<IDomainEvent> DomainEvents { get; }

    /// <summary>Clears pending domain events once they have been dispatched.</summary>
    void ClearDomainEvents();
}
