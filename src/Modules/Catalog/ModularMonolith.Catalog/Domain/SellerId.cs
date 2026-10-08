using ModularMonolith.Shared.Kernel.Results;

namespace ModularMonolith.Catalog.Domain;

/// <summary>
/// Value object: the identity-provider subject of the user who lists a product. Catalog's own name
/// for the concept; it matches <c>SubjectId</c> in the Identity module's integration events.
/// </summary>
internal sealed record SellerId
{
    public const int MaxLength = 255;

    private SellerId(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static Result<SellerId> Create(string? value)
        => string.IsNullOrWhiteSpace(value) || value.Length > MaxLength
            ? Result.Failure<SellerId>(CatalogErrors.SellerInvalid)
            : Result.Success(new SellerId(value));

    /// <inheritdoc />
    public override string ToString() => Value;
}
