using System.Text.RegularExpressions;
using ModularMonolith.Shared.Kernel.Results;

namespace ModularMonolith.Catalog.Domain;

/// <summary>
/// Value object: a positive amount in an ISO 4217 currency, rounded to two decimals.
/// </summary>
internal sealed partial record Money
{
    private Money(decimal amount, string currency)
    {
        Amount = amount;
        Currency = currency;
    }

    public decimal Amount { get; }

    public string Currency { get; }

    public static Result<Money> Create(decimal amount, string? currency)
    {
        if (amount <= 0)
        {
            return Result.Failure<Money>(CatalogErrors.PriceInvalid);
        }

        var normalized = currency?.Trim().ToUpperInvariant();
        if (normalized is null || !CurrencyPattern().IsMatch(normalized))
        {
            return Result.Failure<Money>(CatalogErrors.CurrencyInvalid);
        }

        return Result.Success(new Money(decimal.Round(amount, 2, MidpointRounding.ToEven), normalized));
    }

    [GeneratedRegex("^[A-Z]{3}$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex CurrencyPattern();
}
