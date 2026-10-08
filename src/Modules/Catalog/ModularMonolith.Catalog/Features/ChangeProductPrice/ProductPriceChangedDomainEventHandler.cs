using Microsoft.AspNetCore.OutputCaching;
using ModularMonolith.Catalog.Domain;
using ModularMonolith.Catalog.Infrastructure;
using ModularMonolith.Shared.Infrastructure.Caching;
using ModularMonolith.Shared.Infrastructure.Data;
using ModularMonolith.Shared.Kernel.Context;
using ModularMonolith.Shared.Kernel.Cqrs;

namespace ModularMonolith.Catalog.Features.ChangeProductPrice;

/// <summary>
/// Once the price change is committed: drops the tenant's cached product-list pages and cached price
/// responses, and counts the change.
/// </summary>
internal sealed class ProductPriceChangedDomainEventHandler(
    IUnitOfWork unitOfWork,
    ICacheStore cache,
    IOutputCacheStore outputCache,
    ITenantContext tenantContext) : IDomainEventHandler<ProductPriceChangedDomainEvent>
{
    public Task HandleAsync(ProductPriceChangedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        var priceTag = CatalogOutputCache.PriceTag(tenantContext.TenantId);
        unitOfWork.OnCommitted(async ct =>
        {
            await cache.InvalidateAsync(CatalogCache.ProductList, ct);
            await outputCache.EvictByTagAsync(priceTag, ct);
            CatalogTelemetry.PriceChanges.Add(1);
        });
        return Task.CompletedTask;
    }
}
