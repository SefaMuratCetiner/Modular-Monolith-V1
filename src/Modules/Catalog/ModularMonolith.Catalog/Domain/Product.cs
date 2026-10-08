using System.Text.RegularExpressions;
using ModularMonolith.Shared.Kernel.Domain;
using ModularMonolith.Shared.Kernel.Results;

namespace ModularMonolith.Catalog.Domain;

/// <summary>
/// Product aggregate. Invariants live here (not only in validators), so an invalid product
/// cannot be constructed from any entry point: HTTP, message handler or test.
/// </summary>
internal sealed partial class Product : AggregateRoot
{
    public const int MaxNameLength = 200;

    private Product(Guid id, string name, string sku, Money price, string sellerId)
        : base(id)
    {
        Name = name;
        Sku = sku;
        Price = price;
        SellerId = sellerId;
    }

    public string Name { get; }

    public string Sku { get; }

    public Money Price { get; }

    /// <summary>Gets the subject id of the user who listed the product.</summary>
    public string SellerId { get; }

    /// <summary>Creates a product and raises <see cref="ProductCreatedDomainEvent"/>.</summary>
    public static Result<Product> Create(string? name, string? sku, decimal amount, string? currency, string sellerId, DateTimeOffset now)
    {
        var trimmedName = name?.Trim();
        if (string.IsNullOrEmpty(trimmedName) || trimmedName.Length > MaxNameLength)
        {
            return Result.Failure<Product>(CatalogErrors.NameInvalid);
        }

        var normalizedSku = sku?.Trim().ToUpperInvariant();
        if (normalizedSku is null || !SkuPattern().IsMatch(normalizedSku))
        {
            return Result.Failure<Product>(CatalogErrors.SkuInvalid);
        }

        var price = Money.Create(amount, currency);
        if (price.IsFailure)
        {
            return Result.Failure<Product>(price.Error);
        }

        var product = new Product(Guid.CreateVersion7(now), trimmedName, normalizedSku, price.Value, sellerId);
        product.Raise(new ProductCreatedDomainEvent(
            Guid.CreateVersion7(now),
            now,
            product.Id,
            product.Name,
            product.Sku,
            product.Price.Amount,
            product.Price.Currency,
            product.SellerId));
        return Result.Success(product);
    }

    [GeneratedRegex("^[A-Z0-9][A-Z0-9-]{2,63}$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex SkuPattern();
}
