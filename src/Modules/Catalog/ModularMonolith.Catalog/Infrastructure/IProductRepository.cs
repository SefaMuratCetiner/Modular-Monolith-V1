using ModularMonolith.Catalog.Domain;

namespace ModularMonolith.Catalog.Infrastructure;

/// <summary>
/// Write-side persistence for the <see cref="Product"/> aggregate.
/// Read-side queries live in their slices and use <c>IDbSession</c> directly.
/// </summary>
internal interface IProductRepository
{
    Task<bool> SkuExistsAsync(string sku, CancellationToken cancellationToken);

    Task AddAsync(Product product, CancellationToken cancellationToken);
}
