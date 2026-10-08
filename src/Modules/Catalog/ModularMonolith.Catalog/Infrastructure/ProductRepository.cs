using Dapper;
using ModularMonolith.Catalog.Domain;
using ModularMonolith.Shared.Infrastructure.Data;
using ModularMonolith.Shared.Kernel.Context;

namespace ModularMonolith.Catalog.Infrastructure;

/// <summary>
/// Dapper repository. It never opens its own connection or transaction: it joins the scoped
/// session/unit of work, and tenant_id is filled and enforced by PostgreSQL (default + RLS).
/// </summary>
internal sealed class ProductRepository(
    IDbSession session,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : IProductRepository
{
    public async Task<bool> SkuExistsAsync(string sku, CancellationToken cancellationToken)
    {
        var connection = await session.GetConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "select exists (select 1 from catalog.products where sku = @Sku and not is_deleted)",
            new { Sku = sku.Trim().ToUpperInvariant() },
            session.Transaction,
            cancellationToken: cancellationToken));
    }

    public async Task AddAsync(Product product, CancellationToken cancellationToken)
    {
        product.MarkCreated(currentUser.AuditId, timeProvider.GetUtcNow());
        var connection = await session.GetConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            insert into catalog.products (id, name, sku, price, currency, seller_id, created_by, created_at)
            values (@Id, @Name, @Sku, @Amount, @Currency, @SellerId, @CreatedBy, @CreatedAt)
            """,
            new
            {
                product.Id,
                product.Name,
                product.Sku,
                product.Price.Amount,
                product.Price.Currency,
                product.SellerId,
                product.CreatedBy,
                product.CreatedAt,
            },
            session.Transaction,
            cancellationToken: cancellationToken));

        // Domain events are dispatched by the transactional decorator before commit.
        unitOfWork.Track(product);
    }
}
