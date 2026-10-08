using ModularMonolith.Shared.Kernel.Cqrs;
using ModularMonolith.Shared.Kernel.Paging;

namespace ModularMonolith.Catalog.Features.ListProducts;

/// <summary>Lists one page of the current tenant's products, ordered by name.</summary>
internal sealed record ListProductsQuery(PageRequest Paging) : IQuery<PagedResult<ProductListItem>>;

/// <summary>Read model returned by the slice (separate from the aggregate).</summary>
internal sealed record ProductListItem(Guid Id, string Name, string Sku, decimal Price, string Currency, string SellerId, string? SellerName);
