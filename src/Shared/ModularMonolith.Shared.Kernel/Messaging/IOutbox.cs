namespace ModularMonolith.Shared.Kernel.Messaging;

/// <summary>
/// Transactional outbox. Writes are part of the current unit of work, so an integration event
/// is persisted if and only if the business change that produced it is committed.
/// </summary>
public interface IOutbox
{
    /// <summary>Adds an integration event to the publishing module's outbox table.</summary>
    /// <param name="integrationEvent">The event.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    Task AddAsync(IIntegrationEvent integrationEvent, CancellationToken cancellationToken);
}
