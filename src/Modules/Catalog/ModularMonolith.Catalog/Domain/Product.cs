using ModularMonolith.Shared.Kernel.Domain;
using ModularMonolith.Shared.Kernel.Results;

namespace ModularMonolith.Catalog.Domain;

/// <summary>
/// Product aggregate. Invariants live here (not only in validators), so an invalid product
/// cannot be constructed or changed from any entry point: HTTP, message handler or test.
/// Identity and the meaningful primitives are value objects (<see cref="ProductId"/>, <see cref="Sku"/>,
/// <see cref="Money"/>, <see cref="SellerId"/>).
/// </summary>
internal sealed class Product : AggregateRoot<ProductId>
{
    public const int MaxNameLength = 200;

    private Product(ProductId id, string name, Sku sku, Money price, SellerId sellerId)
        : base(id)
    {
        Name = name;
        Sku = sku;
        Price = price;
        SellerId = sellerId;
    }

    public string Name { get; }

    public Sku Sku { get; }

    public Money Price { get; private set; }

    /// <summary>Gets the subject id of the user who listed the product.</summary>
    public SellerId SellerId { get; }

    /// <summary>Creates a product and raises <see cref="ProductCreatedDomainEvent"/>.</summary>
    public static Result<Product> Create(string? name, string? sku, decimal amount, string? currency, SellerId sellerId, DateTimeOffset now)
    {
        var trimmedName = name?.Trim();
        if (string.IsNullOrEmpty(trimmedName) || trimmedName.Length > MaxNameLength)
        {
            return Result.Failure<Product>(CatalogErrors.NameInvalid);
        }

        var validSku = Sku.Create(sku);
        if (validSku.IsFailure)
        {
            return Result.Failure<Product>(validSku.Error);
        }

        var price = Money.Create(amount, currency);
        if (price.IsFailure)
        {
            return Result.Failure<Product>(price.Error);
        }

        var product = new Product(ProductId.New(now), trimmedName, validSku.Value, price.Value, sellerId);
        product.Raise(new ProductCreatedDomainEvent(
            Guid.CreateVersion7(now),
            now,
            product.Id,
            product.Name,
            product.Sku.Value,
            product.Price.Amount,
            product.Price.Currency,
            product.SellerId.Value));
        return Result.Success(product);
    }

    /// <summary>Rebuilds a product from storage (no events, no validation of historical data).</summary>
    public static Product Rehydrate(
        ProductId id,
        string name,
        string sku,
        decimal amount,
        string currency,
        string sellerId,
        int version,
        string createdBy,
        DateTimeOffset createdAt,
        string? modifiedBy,
        DateTimeOffset? modifiedAt)
    {
        var product = new Product(id, name, Sku.Create(sku).Value, Money.Create(amount, currency).Value, SellerId.Create(sellerId).Value);
        product.RestorePersistedState(version, createdBy, createdAt, modifiedBy, modifiedAt);
        return product;
    }

    /// <summary>Changes the price and raises <see cref="ProductPriceChangedDomainEvent"/>.</summary>
    public Result ChangePrice(decimal amount, string? currency, DateTimeOffset now)
    {
        var price = Money.Create(amount, currency);
        if (price.IsFailure)
        {
            return Result.Failure(price.Error);
        }

        if (price.Value == Price)
        {
            return Result.Success();
        }

        var previous = Price;
        Price = price.Value;
        Raise(new ProductPriceChangedDomainEvent(Guid.CreateVersion7(now), now, Id, previous.Amount, previous.Currency, Price.Amount, Price.Currency));
        return Result.Success();
    }
}
