using ModularMonolith.Catalog.Contracts;
using ModularMonolith.Catalog.Domain;
using ModularMonolith.Catalog.Features.ListProducts;
using ModularMonolith.Shared.Infrastructure.Caching;
using ModularMonolith.Shared.Infrastructure.Data;
using ModularMonolith.Shared.Kernel.Context;
using ModularMonolith.Shared.Kernel.Cqrs;
using ModularMonolith.Shared.Kernel.Messaging;

namespace ModularMonolith.Catalog.Features.CreateProduct;

/// <summary>
/// Domain event handler running inside the CreateProduct transaction:
/// <list type="number">
/// <item>maps the internal domain event to the public integration event and writes it to the outbox;</item>
/// <item>invalidates every cached page of the tenant's product list, but only after a successful commit.</item>
/// </list>
/// </summary>
internal sealed class PublishProductCreated(
    IOutbox outbox,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork,
    ICacheStore cache) : IDomainEventHandler<ProductCreatedDomainEvent>
{
    public async Task HandleAsync(ProductCreatedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        await outbox.AddAsync(
            new ProductCreatedIntegrationEvent(
                domainEvent.EventId,
                domainEvent.OccurredAt,
                tenantContext.TenantId,
                domainEvent.ProductId,
                domainEvent.Name,
                domainEvent.Sku,
                domainEvent.Price,
                domainEvent.Currency,
                domainEvent.SellerId),
            cancellationToken);

        unitOfWork.OnCommitted(ct => cache.InvalidateRegionAsync(ListProductsQueryHandler.CacheRegion, ct));
    }
}
