using System.Text.Json;
using ModularMonolith.Shared.Infrastructure.Modules;
using ModularMonolith.Shared.Kernel.Messaging;

namespace ModularMonolith.Shared.Infrastructure.Messaging;

/// <summary>
/// Maps stable event names to integration event types found in the modules' Contracts assemblies.
/// Only contract types can be deserialized from the outbox (no arbitrary type loading).
/// </summary>
public sealed class IntegrationEventRegistry
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly Dictionary<string, Type> _typesByName;

    /// <summary>Initializes a new instance of the <see cref="IntegrationEventRegistry"/> class.</summary>
    /// <param name="modules">The module catalog.</param>
    public IntegrationEventRegistry(ModuleCatalog modules)
    {
        ArgumentNullException.ThrowIfNull(modules);
        _typesByName = modules.Modules
            .SelectMany(module => module.ContractsAssembly.GetExportedTypes())
            .Where(type => type is { IsClass: true, IsAbstract: false } && typeof(IIntegrationEvent).IsAssignableFrom(type))
            .ToDictionary(NameOf, type => type, StringComparer.Ordinal);
    }

    /// <summary>Gets the stable name stored in the outbox for an event type.</summary>
    /// <param name="eventType">The event type.</param>
    /// <returns>The name.</returns>
    public static string NameOf(Type eventType)
    {
        ArgumentNullException.ThrowIfNull(eventType);
        return eventType.FullName ?? eventType.Name;
    }

    /// <summary>Serializes an integration event payload.</summary>
    /// <param name="integrationEvent">The event.</param>
    /// <returns>The JSON payload.</returns>
    public static string Serialize(IIntegrationEvent integrationEvent)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        return JsonSerializer.Serialize(integrationEvent, integrationEvent.GetType(), JsonOptions);
    }

    /// <summary>Deserializes an outbox payload into its integration event type.</summary>
    /// <param name="name">The stored event name.</param>
    /// <param name="payload">The JSON payload.</param>
    /// <returns>The event.</returns>
    public IIntegrationEvent Deserialize(string name, string payload)
    {
        if (!_typesByName.TryGetValue(name, out var type))
        {
            throw new InvalidOperationException($"Unknown integration event type '{name}'.");
        }

        return (IIntegrationEvent?)JsonSerializer.Deserialize(payload, type, JsonOptions)
            ?? throw new InvalidOperationException($"Payload for '{name}' deserialized to null.");
    }
}
