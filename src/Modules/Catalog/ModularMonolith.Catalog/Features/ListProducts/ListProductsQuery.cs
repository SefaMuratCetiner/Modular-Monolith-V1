using ModularMonolith.Shared.Kernel.Cqrs;

namespace ModularMonolith.Catalog.Features.ListProducts;

/// <summary>Lists the current tenant's products ordered by name.</summary>
internal sealed record ListProductsQuery : IQuery<IReadOnlyList<ProductListItem>>;

/// <summary>Read model returned by the slice (separate from the aggregate).</summary>
internal sealed record ProductListItem(Guid Id, string Name, string Sku, decimal Price, string Currency, string SellerId, string? SellerName);
