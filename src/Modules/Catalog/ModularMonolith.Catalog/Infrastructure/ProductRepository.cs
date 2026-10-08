using Dapper;
using ModularMonolith.Catalog.Domain;
using ModularMonolith.Shared.Infrastructure.Data;
using ModularMonolith.Shared.Kernel.Context;

namespace ModularMonolith.Catalog.Infrastructure;

/// <summary>
/// Dapper repository. It never opens its own connection or transaction: it joins the scoped
/// session/unit of work, and tenant_id is filled and enforced by PostgreSQL (default + RLS).
/// Every write records a before/after snapshot for the audit log and every update is
/// conditional on the version (optimistic concurrency).
/// </summary>
internal sealed class ProductRepository(
    IDbSession session,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : IProductRepository
{
    private const string EntityType = "product";

    public async Task<bool> SkuExistsAsync(Sku sku, CancellationToken cancellationToken)
    {
        var connection = await session.GetConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "select exists (select 1 from catalog.products where sku = @Sku and not is_deleted)",
            new { Sku = sku.Value },
            session.Transaction,
            cancellationToken: cancellationToken));
    }

    public async Task<Product?> GetAsync(ProductId id, CancellationToken cancellationToken)
    {
        var connection = await session.GetConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<ProductRow>(new CommandDefinition(
            """
            select id as Id, name as Name, sku as Sku, price as Price, currency as Currency, seller_id as SellerId,
                   version as Version, created_by as CreatedBy, created_at as CreatedAt,
                   modified_by as ModifiedBy, modified_at as ModifiedAt
            from catalog.products
            where id = @Id and not is_deleted
            """,
            new { Id = id.Value },
            session.Transaction,
            cancellationToken: cancellationToken));

        return row is null
            ? null
            : Product.Rehydrate(
                new ProductId(row.Id), row.Name, row.Sku, row.Price, row.Currency, row.SellerId,
                row.Version, row.CreatedBy, row.CreatedAt, row.ModifiedBy, row.ModifiedAt);
    }

    public async Task AddAsync(Product product, CancellationToken cancellationToken)
    {
        product.MarkCreated(currentUser.AuditId, timeProvider.GetUtcNow());
        var connection = await session.GetConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            insert into catalog.products (id, name, sku, price, currency, seller_id, version, created_by, created_at)
            values (@Id, @Name, @Sku, @Amount, @Currency, @SellerId, @Version, @CreatedBy, @CreatedAt)
            """,
            new
            {
                Id = product.Id.Value,
                product.Name,
                Sku = product.Sku.Value,
                product.Price.Amount,
                product.Price.Currency,
                SellerId = product.SellerId.Value,
                product.Version,
                product.CreatedBy,
                product.CreatedAt,
            },
            session.Transaction,
            cancellationToken: cancellationToken));

        unitOfWork.RecordChange(new AuditChange(EntityType, product.Id.ToString(), AuditChange.Create, Before: null, After: Snapshot(product, product.Version)));

        // Domain events are dispatched by the transactional decorator before commit.
        unitOfWork.Track(product);
    }

    public async Task<bool> UpdatePriceAsync(Product product, CancellationToken cancellationToken)
    {
        var connection = await session.GetConnectionAsync(cancellationToken);
        var before = await connection.QuerySingleOrDefaultAsync<ProductSnapshot>(new CommandDefinition(
            "select name as Name, sku as Sku, price as Price, currency as Currency, version as Version from catalog.products where id = @Id",
            new { Id = product.Id.Value },
            session.Transaction,
            cancellationToken: cancellationToken));

        product.MarkModified(currentUser.AuditId, timeProvider.GetUtcNow());
        var updated = await connection.ExecuteAsync(new CommandDefinition(
            """
            update catalog.products
            set price = @Amount, currency = @Currency, modified_by = @ModifiedBy, modified_at = @ModifiedAt, version = version + 1
            where id = @Id and version = @Version and not is_deleted
            """,
            new
            {
                Id = product.Id.Value,
                product.Price.Amount,
                product.Price.Currency,
                product.ModifiedBy,
                product.ModifiedAt,
                product.Version,
            },
            session.Transaction,
            cancellationToken: cancellationToken));

        if (updated == 0)
        {
            return false;
        }

        unitOfWork.RecordChange(new AuditChange(EntityType, product.Id.ToString(), AuditChange.Update, before, Snapshot(product, product.Version + 1)));
        unitOfWork.Track(product);
        return true;
    }

    private static ProductSnapshot Snapshot(Product product, int version)
        => new(product.Name, product.Sku.Value, product.Price.Amount, product.Price.Currency, version);

    /// <summary>The audited fields of a product.</summary>
    private sealed record ProductSnapshot(string Name, string Sku, decimal Price, string Currency, int Version);

    private sealed class ProductRow
    {
        public Guid Id { get; init; }

        public string Name { get; init; } = string.Empty;

        public string Sku { get; init; } = string.Empty;

        public decimal Price { get; init; }

        public string Currency { get; init; } = string.Empty;

        public string SellerId { get; init; } = string.Empty;

        public int Version { get; init; }

        public string CreatedBy { get; init; } = string.Empty;

        public DateTimeOffset CreatedAt { get; init; }

        public string? ModifiedBy { get; init; }

        public DateTimeOffset? ModifiedAt { get; init; }
    }
}
