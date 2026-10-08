using System.Text.RegularExpressions;
using ModularMonolith.Shared.Kernel.Results;

namespace ModularMonolith.Identity.Domain;

/// <summary>
/// Value object: a syntactically valid e-mail address (trimmed, at most 254 characters).
/// Equality is case-insensitive, matching the unique index on <c>lower(email)</c>.
/// </summary>
internal sealed partial record Email
{
    public const int MaxLength = 254;

    private Email(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static Result<Email> Create(string? value)
    {
        var trimmed = value?.Trim();
        return trimmed is not null && trimmed.Length <= MaxLength && EmailPattern().IsMatch(trimmed)
            ? Result.Success(new Email(trimmed))
            : Result.Failure<Email>(IdentityErrors.EmailInvalid);
    }

    /// <inheritdoc />
    public bool Equals(Email? other) => other is not null && string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Value);

    /// <inheritdoc />
    public override string ToString() => Value;

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex EmailPattern();
}
