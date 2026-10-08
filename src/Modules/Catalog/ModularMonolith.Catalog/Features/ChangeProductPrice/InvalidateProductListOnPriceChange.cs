using ModularMonolith.Catalog.Domain;
using ModularMonolith.Catalog.Infrastructure;
using ModularMonolith.Shared.Infrastructure.Caching;
using ModularMonolith.Shared.Infrastructure.Data;
using ModularMonolith.Shared.Kernel.Cqrs;

namespace ModularMonolith.Catalog.Features.ChangeProductPrice;

/// <summary>Drops every cached product-list page of the tenant once the price change is committed.</summary>
internal sealed class InvalidateProductListOnPriceChange(IUnitOfWork unitOfWork, ICacheStore cache)
    : IDomainEventHandler<ProductPriceChangedDomainEvent>
{
    public Task HandleAsync(ProductPriceChangedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        unitOfWork.OnCommitted(ct => cache.InvalidateAsync(CatalogCache.ProductList, ct));
        return Task.CompletedTask;
    }
}
