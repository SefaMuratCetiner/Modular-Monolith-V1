using ModularMonolith.Shared.Kernel.Messaging;

namespace ModularMonolith.Catalog.Contracts;

/// <summary>
/// Published by Catalog after a product has been created (delivered via the transactional outbox).
/// </summary>
/// <param name="EventId">The event identifier.</param>
/// <param name="OccurredAt">When the product was created.</param>
/// <param name="TenantId">The owning tenant.</param>
/// <param name="ProductId">The product identifier.</param>
/// <param name="Name">The product name.</param>
/// <param name="Sku">The stock keeping unit.</param>
/// <param name="Price">The price amount.</param>
/// <param name="Currency">The ISO 4217 currency code.</param>
/// <param name="SellerId">The subject id of the user who created the product.</param>
[IntegrationEvent("catalog.product-created", version: 1)]
public sealed record ProductCreatedIntegrationEvent(
    Guid EventId,
    DateTimeOffset OccurredAt,
    string TenantId,
    Guid ProductId,
    string Name,
    string Sku,
    decimal Price,
    string Currency,
    string SellerId) : IIntegrationEvent;
