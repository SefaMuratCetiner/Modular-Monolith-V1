using Dapper;
using ModularMonolith.Catalog.Domain;
using ModularMonolith.Shared.Infrastructure.Data;
using ModularMonolith.Shared.Kernel.Cqrs;
using ModularMonolith.Shared.Kernel.Results;

namespace ModularMonolith.Catalog.Features.GetProduct;

/// <summary>Reads one product (RLS-scoped: another tenant's product is simply "not found").</summary>
internal sealed class GetProductQueryHandler(IDbSession session) : IQueryHandler<GetProductQuery, ProductDetails>
{
    public async Task<Result<ProductDetails>> HandleAsync(GetProductQuery query, CancellationToken cancellationToken)
    {
        var connection = await session.GetConnectionAsync(cancellationToken);
        var product = await connection.QuerySingleOrDefaultAsync<ProductDetails>(new CommandDefinition(
            """
            select p.id as Id, p.name as Name, p.sku as Sku, p.price as Price, p.currency as Currency,
                   p.seller_id as SellerId, s.display_name as SellerName
            from catalog.products p
            left join catalog.sellers s on s.subject_id = p.seller_id
            where p.id = @Id and not p.is_deleted
            """,
            new { Id = query.ProductId },
            cancellationToken: cancellationToken));

        return product is null
            ? Result.Failure<ProductDetails>(CatalogErrors.ProductNotFound)
            : Result.Success(product);
    }
}
