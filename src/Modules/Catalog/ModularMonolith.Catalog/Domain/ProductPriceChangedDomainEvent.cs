using ModularMonolith.Shared.Kernel.Domain;

namespace ModularMonolith.Catalog.Domain;

/// <summary>Internal domain event raised when a product's price changes.</summary>
internal sealed record ProductPriceChangedDomainEvent(
    Guid EventId,
    DateTimeOffset OccurredAt,
    ProductId ProductId,
    decimal PreviousPrice,
    string PreviousCurrency,
    decimal Price,
    string Currency) : IDomainEvent;
