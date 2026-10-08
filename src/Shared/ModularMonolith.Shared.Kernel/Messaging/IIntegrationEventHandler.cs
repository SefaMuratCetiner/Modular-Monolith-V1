namespace ModularMonolith.Shared.Kernel.Messaging;

/// <summary>
/// Consumes an integration event published by another module.
/// Each handler runs in its own scope and transaction and is protected by an inbox,
/// so it is executed at most once per message even when delivery is retried.
/// </summary>
/// <typeparam name="TEvent">The integration event type.</typeparam>
public interface IIntegrationEventHandler<in TEvent>
    where TEvent : IIntegrationEvent
{
    /// <summary>Handles the integration event.</summary>
    /// <param name="integrationEvent">The event.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    Task HandleAsync(TEvent integrationEvent, CancellationToken cancellationToken);
}
