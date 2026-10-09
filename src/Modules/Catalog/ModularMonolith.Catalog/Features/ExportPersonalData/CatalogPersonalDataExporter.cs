using Dapper;
using ModularMonolith.Shared.Infrastructure.Data;
using ModularMonolith.Shared.Kernel.Privacy;

namespace ModularMonolith.Catalog.Features.ExportPersonalData;

/// <summary>
/// Catalog's part of a personal-data export: the seller projection and the products the user listed
/// in the current tenant.
/// </summary>
internal sealed class CatalogPersonalDataExporter(IDbSession session) : IPersonalDataExporter
{
    public string Section => "catalog";

    public async Task<object?> ExportAsync(string subjectId, CancellationToken cancellationToken)
    {
        var connection = await session.GetConnectionAsync(cancellationToken);
        var seller = await connection.QuerySingleOrDefaultAsync<SellerData>(new CommandDefinition(
            """
            select display_name as DisplayName, registered_at as RegisteredAt, erased_at as ErasedAt
            from catalog.sellers
            where subject_id = @SubjectId
            """,
            new { SubjectId = subjectId },
            cancellationToken: cancellationToken));

        var products = await connection.QueryAsync<ListedProduct>(new CommandDefinition(
            """
            select id as Id, name as Name, sku as Sku, created_at as ListedAt
            from catalog.products
            where seller_id = @SubjectId and not is_deleted
            order by created_at, id
            """,
            new { SubjectId = subjectId },
            cancellationToken: cancellationToken));

        var listed = products.AsList();
        return seller is null && listed.Count == 0 ? null : new CatalogPersonalData(seller, listed);
    }

    /// <summary>The exported Catalog data.</summary>
    private sealed record CatalogPersonalData(SellerData? Seller, IReadOnlyList<ListedProduct> Products);

    /// <summary>The seller projection.</summary>
    private sealed record SellerData(string DisplayName, DateTimeOffset RegisteredAt, DateTimeOffset? ErasedAt);

    /// <summary>A product the user listed.</summary>
    private sealed record ListedProduct(Guid Id, string Name, string Sku, DateTimeOffset ListedAt);
}
