using System.Text.RegularExpressions;

namespace ModularMonolith.Shared.Infrastructure.Data;

/// <summary>
/// Validates and quotes SQL identifiers (schemas, roles) that cannot be passed as parameters.
/// Only lowercase identifiers are accepted, so two different inputs can never map to the same name.
/// </summary>
public static partial class SqlIdentifier
{
    /// <summary>Validates and double-quotes an identifier.</summary>
    /// <param name="identifier">The identifier.</param>
    /// <returns>The quoted identifier.</returns>
    /// <exception cref="ArgumentException">The identifier is not a safe lowercase identifier.</exception>
    public static string Quote(string identifier)
    {
        if (string.IsNullOrEmpty(identifier) || !IdentifierPattern().IsMatch(identifier))
        {
            throw new ArgumentException($"'{identifier}' is not a valid lowercase SQL identifier.", nameof(identifier));
        }

        return $"\"{identifier}\"";
    }

    [GeneratedRegex("^[a-z_][a-z0-9_]{0,62}$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex IdentifierPattern();
}
