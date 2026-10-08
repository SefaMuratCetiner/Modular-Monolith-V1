using ModularMonolith.Shared.Kernel.Results;

namespace ModularMonolith.Catalog.Domain;

/// <summary>Catalog error catalogue: stable codes the API exposes in problem details.</summary>
internal static class CatalogErrors
{
    public static Error NameInvalid { get; } = Error.Validation("catalog.name_invalid", $"Product name is required and must be at most {Product.MaxNameLength} characters.");

    public static Error SkuInvalid { get; } = Error.Validation("catalog.sku_invalid", "SKU must be 3-64 characters: letters, digits and dashes.");

    public static Error PriceInvalid { get; } = Error.Validation("catalog.price_invalid", "Price must be greater than zero.");

    public static Error CurrencyInvalid { get; } = Error.Validation("catalog.currency_invalid", "Currency must be a 3-letter ISO 4217 code.");

    public static Error SellerInvalid { get; } = Error.Forbidden("catalog.seller_invalid", "Products can only be listed by an authenticated user.");

    public static Error SkuTaken { get; } = Error.Conflict("catalog.sku_taken", "A product with this SKU already exists.");

    public static Error ProductNotFound { get; } = Error.NotFound("catalog.product_not_found", "The product does not exist.");

    public static Error WritesDisabled { get; } = Error.Unavailable("catalog.writes_disabled", "Catalog writes are temporarily disabled.");

    public static Error ExchangeRateUnavailable { get; } = Error.Unavailable("catalog.exchange_rate_unavailable", "Exchange rates are currently unavailable.");
}
