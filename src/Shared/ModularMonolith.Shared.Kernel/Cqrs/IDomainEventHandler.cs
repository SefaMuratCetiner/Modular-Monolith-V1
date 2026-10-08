using ModularMonolith.Shared.Kernel.Domain;

namespace ModularMonolith.Shared.Kernel.Cqrs;

/// <summary>
/// Reacts to a domain event inside the same transaction that raised it.
/// Typical use: translate the domain event into an integration event and write it to the outbox.
/// </summary>
/// <typeparam name="TEvent">The domain event type.</typeparam>
public interface IDomainEventHandler<in TEvent>
    where TEvent : IDomainEvent
{
    /// <summary>Handles the domain event.</summary>
    /// <param name="domainEvent">The event.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    Task HandleAsync(TEvent domainEvent, CancellationToken cancellationToken);
}
