using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Localization;

namespace ModularMonolith.Shared.Infrastructure.Localization;

/// <summary>
/// Reads flat JSON resources embedded in a module assembly (<c>Resources/{Module}.{culture}.json</c>).
/// Falls back through parent cultures to <c>en</c>, then to the key itself.
/// </summary>
internal sealed class JsonStringLocalizer(Assembly assembly) : IStringLocalizer
{
    private const string FallbackCulture = "en";

    private readonly ConcurrentDictionary<string, IReadOnlyDictionary<string, string>> _cultures = new(StringComparer.OrdinalIgnoreCase);

    public LocalizedString this[string name]
    {
        get
        {
            var value = Find(name);
            return new LocalizedString(name, value ?? name, resourceNotFound: value is null);
        }
    }

    public LocalizedString this[string name, params object[] arguments]
    {
        get
        {
            var value = Find(name);
            var formatted = string.Format(CultureInfo.CurrentCulture, value ?? name, arguments);
            return new LocalizedString(name, formatted, resourceNotFound: value is null);
        }
    }

    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
        => Load(CultureInfo.CurrentUICulture.Name).Select(pair => new LocalizedString(pair.Key, pair.Value, resourceNotFound: false));

    private string? Find(string name)
    {
        for (var culture = CultureInfo.CurrentUICulture; ; culture = culture.Parent)
        {
            var cultureName = string.IsNullOrEmpty(culture.Name) ? FallbackCulture : culture.Name;
            if (Load(cultureName).TryGetValue(name, out var value))
            {
                return value;
            }

            if (string.IsNullOrEmpty(culture.Name))
            {
                return null;
            }
        }
    }

    private IReadOnlyDictionary<string, string> Load(string cultureName) => _cultures.GetOrAdd(cultureName, ReadResource);

    private Dictionary<string, string> ReadResource(string cultureName)
    {
        var suffix = $".{cultureName}.json";
        var resourceName = Array.Find(
            assembly.GetManifestResourceNames(),
            name => name.Contains(".Resources.", StringComparison.Ordinal) && name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
        if (resourceName is null)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        return JsonSerializer.Deserialize<Dictionary<string, string>>(stream)
            ?? new Dictionary<string, string>(StringComparer.Ordinal);
    }
}
