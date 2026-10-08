using Dapper;
using Microsoft.Extensions.Options;
using ModularMonolith.Shared.Infrastructure.Caching;
using ModularMonolith.Shared.Infrastructure.Data;
using ModularMonolith.Shared.Kernel.Cqrs;
using ModularMonolith.Shared.Kernel.Paging;
using ModularMonolith.Shared.Kernel.Results;

namespace ModularMonolith.Catalog.Features.ListProducts;

/// <summary>
/// Query side of the slice: plain SQL against the read model, cached per tenant and page in Valkey.
/// RLS guarantees only the current tenant's rows are visible, even without a WHERE clause.
/// All pages share one cache region, so creating a product invalidates every cached page at once.
/// </summary>
internal sealed class ListProductsQueryHandler(
    IDbSession session,
    ICacheStore cache,
    IOptions<CatalogOptions> options) : IQueryHandler<ListProductsQuery, PagedResult<ProductListItem>>
{
    public const string CacheRegion = "catalog:products:list";

    private const string CountSql = "select count(*) from catalog.products where not is_deleted";

    // "order by name, id" keeps paging deterministic when names repeat.
    private const string PageSql = """
        select p.id as Id, p.name as Name, p.sku as Sku, p.price as Price, p.currency as Currency,
               p.seller_id as SellerId, s.display_name as SellerName
        from catalog.products p
        left join catalog.sellers s on s.subject_id = p.seller_id
        where not p.is_deleted
        order by p.name, p.id
        limit @Limit offset @Offset
        """;

    public async Task<Result<PagedResult<ProductListItem>>> HandleAsync(ListProductsQuery query, CancellationToken cancellationToken)
    {
        var paging = query.Paging;
        var page = await cache.GetOrCreateInRegionAsync(
            CacheRegion,
            $"page:{paging.Page}:size:{paging.PageSize}",
            async ct =>
            {
                var connection = await session.GetConnectionAsync(ct);
                var total = await connection.ExecuteScalarAsync<long>(new CommandDefinition(CountSql, cancellationToken: ct));
                var rows = await connection.QueryAsync<ProductListItem>(new CommandDefinition(
                    PageSql,
                    new { Limit = paging.PageSize, paging.Offset },
                    cancellationToken: ct));
                return PagedResult.Create(paging, rows.AsList(), total);
            },
            TimeSpan.FromSeconds(options.Value.ProductCacheSeconds),
            cancellationToken);

        return Result.Success(page);
    }
}
