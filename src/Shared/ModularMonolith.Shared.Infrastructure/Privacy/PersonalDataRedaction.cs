using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using ModularMonolith.Shared.Infrastructure.Serialization;
using ModularMonolith.Shared.Kernel.Privacy;

namespace ModularMonolith.Shared.Infrastructure.Privacy;

/// <summary>
/// Redaction of <see cref="PersonalDataAttribute"/> properties for diagnostic copies of data
/// (audit rows, logs). The owning table keeps the real value; copies never do.
/// </summary>
public static class PersonalDataRedaction
{
    /// <summary>The replacement for a redacted value.</summary>
    public const string Redacted = "[redacted]";

    /// <summary>The display name every module stores for an erased user.</summary>
    public const string ErasedDisplayName = "Erased user";

    private static readonly ConcurrentDictionary<Type, bool> HasPersonalDataCache = new();

    /// <summary>
    /// Gets the audit serializer: <see cref="JsonDefaults"/> conventions, but personal-data strings are
    /// written as <see cref="Redacted"/> and other personal-data values are omitted.
    /// </summary>
    public static JsonSerializerOptions AuditOptions { get; } = CreateAuditOptions();

    /// <summary>Checks whether a property is marked as personal data.</summary>
    /// <param name="property">The property.</param>
    /// <returns><see langword="true"/> when marked.</returns>
    public static bool IsPersonalData(PropertyInfo property)
    {
        ArgumentNullException.ThrowIfNull(property);
        return property.IsDefined(typeof(PersonalDataAttribute), inherit: true);
    }

    /// <summary>Checks (cached) whether a type has at least one personal-data property.</summary>
    /// <param name="type">The type.</param>
    /// <returns><see langword="true"/> when the type has a marked property.</returns>
    public static bool HasPersonalData(Type type)
        => HasPersonalDataCache.GetOrAdd(
            type,
            static candidate => candidate.GetProperties(BindingFlags.Instance | BindingFlags.Public).Any(IsPersonalData));

    private static JsonSerializerOptions CreateAuditOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        JsonDefaults.Apply(options);
        options.TypeInfoResolver = new DefaultJsonTypeInfoResolver { Modifiers = { RedactPersonalData } };
        options.MakeReadOnly();
        return options;
    }

    private static void RedactPersonalData(JsonTypeInfo typeInfo)
    {
        if (typeInfo.Kind != JsonTypeInfoKind.Object)
        {
            return;
        }

        foreach (var property in typeInfo.Properties)
        {
            if (property.AttributeProvider?.IsDefined(typeof(PersonalDataAttribute), inherit: true) != true)
            {
                continue;
            }

            if (property.PropertyType == typeof(string))
            {
                property.Get = static _ => Redacted;
            }
            else
            {
                property.ShouldSerialize = static (_, _) => false;
            }
        }
    }
}
