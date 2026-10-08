namespace ModularMonolith.Shared.Kernel.Messaging;

/// <summary>
/// A public, versionable fact published by one module and consumed by others.
/// Integration events live only in <c>*.Contracts</c> projects and are delivered through the outbox.
/// </summary>
public interface IIntegrationEvent
{
    /// <summary>Gets the unique event identifier (also the outbox and inbox message id).</summary>
    Guid EventId { get; }

    /// <summary>Gets the instant the event occurred.</summary>
    DateTimeOffset OccurredAt { get; }

    /// <summary>Gets the tenant the event belongs to; consumers run in this tenant's context.</summary>
    string TenantId { get; }
}
