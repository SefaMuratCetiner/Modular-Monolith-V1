using Dapper;
using Microsoft.Extensions.Options;
using ModularMonolith.Shared.Infrastructure.Caching;
using ModularMonolith.Shared.Infrastructure.Data;
using ModularMonolith.Shared.Kernel.Cqrs;
using ModularMonolith.Shared.Kernel.Results;

namespace ModularMonolith.Catalog.Features.ListProducts;

/// <summary>
/// Query side of the slice: plain SQL against the read model, cached per tenant in Valkey.
/// RLS guarantees only the current tenant's rows are visible, even without a WHERE clause.
/// </summary>
internal sealed class ListProductsQueryHandler(
    IDbSession session,
    ICacheStore cache,
    IOptions<CatalogOptions> options) : IQueryHandler<ListProductsQuery, IReadOnlyList<ProductListItem>>
{
    public const string CacheKey = "catalog:products:list";

    private const string Sql = """
        select p.id as Id, p.name as Name, p.sku as Sku, p.price as Price, p.currency as Currency,
               p.seller_id as SellerId, s.display_name as SellerName
        from catalog.products p
        left join catalog.sellers s on s.subject_id = p.seller_id
        where not p.is_deleted
        order by p.name
        limit 500
        """;

    public async Task<Result<IReadOnlyList<ProductListItem>>> HandleAsync(ListProductsQuery query, CancellationToken cancellationToken)
    {
        var products = await cache.GetOrCreateAsync<IReadOnlyList<ProductListItem>>(
            CacheKey,
            async ct =>
            {
                var connection = await session.GetConnectionAsync(ct);
                var rows = await connection.QueryAsync<ProductListItem>(new CommandDefinition(Sql, cancellationToken: ct));
                return rows.AsList();
            },
            TimeSpan.FromSeconds(options.Value.ProductCacheSeconds),
            cancellationToken);

        return Result.Success(products);
    }
}
