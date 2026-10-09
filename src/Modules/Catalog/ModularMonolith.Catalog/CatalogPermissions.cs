using ModularMonolith.Shared.Infrastructure.Security;

namespace ModularMonolith.Catalog;

/// <summary>
/// Permissions owned by the Catalog module. Endpoints require them with <c>Policies(...)</c>; each one
/// needs a realm role <b>and</b> an OAuth scope (see <see cref="Permission"/>). Ownership rules (only the
/// seller or a tenant admin may change a product) are checked in the handlers.
/// </summary>
internal static class CatalogPermissions
{
    /// <summary>Read products and prices.</summary>
    public static Permission ProductsRead { get; } = new("catalog.products.read", "catalog.read", [AppRoles.User]);

    /// <summary>Create products and change the price of one's own products.</summary>
    public static Permission ProductsWrite { get; } = new("catalog.products.write", "catalog.write", [AppRoles.User]);

    /// <summary>Gets every Catalog permission (registered by <see cref="CatalogModule"/>).</summary>
    public static Permission[] All { get; } = [ProductsRead, ProductsWrite];
}
