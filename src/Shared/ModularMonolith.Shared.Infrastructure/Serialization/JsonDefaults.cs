using System.Text.Json;
using System.Text.Json.Serialization;

namespace ModularMonolith.Shared.Infrastructure.Serialization;

/// <summary>
/// The single JSON convention for the whole application: HTTP (FastEndpoints and minimal APIs,
/// ProblemDetails), outbox payloads, cache entries and idempotency records.
/// <list type="bullet">
/// <item>camelCase property names, case-insensitive reads.</item>
/// <item>Enums as camelCase strings, never numbers.</item>
/// <item>Numbers must be JSON numbers (no quoted numbers).</item>
/// <item>Null properties are written explicitly so the response shape never changes.</item>
/// </list>
/// </summary>
public static class JsonDefaults
{
    /// <summary>Gets the shared, read-only serializer options.</summary>
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    /// <summary>
    /// Applies the conventions plus strict request parsing for HTTP: a request body containing a property
    /// the endpoint does not declare is rejected with 400 (no silent over-posting or typos). Internal
    /// payloads (outbox, cache) stay tolerant so older and newer versions can read each other.
    /// </summary>
    /// <param name="options">The HTTP serializer options (FastEndpoints and minimal APIs).</param>
    public static void ApplyHttp(JsonSerializerOptions options)
    {
        Apply(options);
        options.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
    }

    /// <summary>Applies the application conventions to an existing options instance.</summary>
    /// <param name="options">The options to configure (e.g. FastEndpoints' or ASP.NET Core's).</param>
    public static void Apply(JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.DictionaryKeyPolicy = null;
        options.PropertyNameCaseInsensitive = true;
        options.NumberHandling = JsonNumberHandling.Strict;
        options.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
        if (!options.Converters.Any(converter => converter is JsonStringEnumConverter))
        {
            options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        }
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        Apply(options);
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }
}
