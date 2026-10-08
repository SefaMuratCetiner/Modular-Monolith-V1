using System.Text.RegularExpressions;
using ModularMonolith.Shared.Kernel.Results;

namespace ModularMonolith.Catalog.Domain;

/// <summary>
/// Value object: a stock keeping unit, normalized to upper case (3-64 letters, digits and dashes).
/// </summary>
internal sealed partial record Sku
{
    private Sku(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static Result<Sku> Create(string? value)
    {
        var normalized = value?.Trim().ToUpperInvariant();
        return normalized is not null && SkuPattern().IsMatch(normalized)
            ? Result.Success(new Sku(normalized))
            : Result.Failure<Sku>(CatalogErrors.SkuInvalid);
    }

    /// <inheritdoc />
    public override string ToString() => Value;

    [GeneratedRegex("^[A-Z0-9][A-Z0-9-]{2,63}$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex SkuPattern();
}
