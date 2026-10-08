namespace ModularMonolith.Shared.Kernel.Domain;

/// <summary>
/// A fact that happened inside an aggregate. Domain events never leave their module;
/// a module translates them into integration events when other modules need to know.
/// </summary>
public interface IDomainEvent
{
    /// <summary>Gets the unique event identifier.</summary>
    Guid EventId { get; }

    /// <summary>Gets the instant the event occurred.</summary>
    DateTimeOffset OccurredAt { get; }
}
