using ModularMonolith.Shared.Kernel.Domain;

namespace ModularMonolith.Catalog.Domain;

/// <summary>
/// Internal domain event. Never leaves the module; it is mapped to
/// <c>ProductCreatedIntegrationEvent</c> by the CreateProduct slice.
/// </summary>
internal sealed record ProductCreatedDomainEvent(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid ProductId,
    string Name,
    string Sku,
    decimal Price,
    string Currency,
    string SellerId) : IDomainEvent;
