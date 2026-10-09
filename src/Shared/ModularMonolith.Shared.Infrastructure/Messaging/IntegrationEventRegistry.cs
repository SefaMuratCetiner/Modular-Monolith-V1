using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using ModularMonolith.Shared.Infrastructure.Modules;
using ModularMonolith.Shared.Infrastructure.Serialization;
using ModularMonolith.Shared.Kernel.Messaging;

namespace ModularMonolith.Shared.Infrastructure.Messaging;

/// <summary>
/// The catalogue of integration event contracts found in the modules' Contracts assemblies.
/// <list type="bullet">
/// <item>The outbox stores the stable versioned name <c>{name}.v{version}</c> from
/// <see cref="IntegrationEventAttribute"/>, never a CLR type name.</item>
/// <item>Older stored versions are upcast step by step with the publisher's
/// <see cref="IIntegrationEventUpcaster"/>s before deserialization.</item>
/// <item>Only registered contract types can be deserialized (no arbitrary type loading).</item>
/// <item>Everything is validated at startup: missing attributes, duplicate names and gaps in an
/// upcaster chain stop the host instead of dead-lettering messages later.</item>
/// </list>
/// Rows written before versioning existed (CLR full name as type) are read as version 1.
/// </summary>
public sealed partial class IntegrationEventRegistry
{
    private readonly Dictionary<string, Contract> _contractsByName;
    private readonly Dictionary<string, Contract> _contractsByLegacyName;
    private readonly Dictionary<(string Name, int FromVersion), IIntegrationEventUpcaster> _upcasters;

    /// <summary>Initializes a new instance of the <see cref="IntegrationEventRegistry"/> class.</summary>
    /// <param name="modules">The module catalog.</param>
    /// <param name="upcasters">The upcasters registered by the modules.</param>
    public IntegrationEventRegistry(ModuleCatalog modules, IEnumerable<IIntegrationEventUpcaster> upcasters)
    {
        ArgumentNullException.ThrowIfNull(modules);
        ArgumentNullException.ThrowIfNull(upcasters);

        Contract[] contracts =
        [
            .. modules.Modules
                .SelectMany(module => module.ContractsAssembly?.GetExportedTypes() ?? [])
                .Where(type => type is { IsClass: true, IsAbstract: false } && typeof(IIntegrationEvent).IsAssignableFrom(type))
                .Select(type => new Contract(type, ContractOf(type))),
        ];

        var duplicate = contracts.GroupBy(contract => contract.Attribute.Name, StringComparer.Ordinal).FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidOperationException($"Integration event name '{duplicate.Key}' is declared by more than one type.");
        }

        _contractsByName = contracts.ToDictionary(contract => contract.Attribute.Name, StringComparer.Ordinal);
        _contractsByLegacyName = contracts.ToDictionary(contract => contract.Type.FullName ?? contract.Type.Name, StringComparer.Ordinal);
        _upcasters = upcasters.ToDictionary(upcaster => (upcaster.EventName, upcaster.FromVersion));

        foreach (var contract in contracts)
        {
            for (var version = 1; version < contract.Attribute.Version; version++)
            {
                if (!_upcasters.ContainsKey((contract.Attribute.Name, version)))
                {
                    throw new InvalidOperationException(
                        $"Integration event '{contract.Attribute.Name}' is at v{contract.Attribute.Version} but has no upcaster from v{version}.");
                }
            }
        }
    }

    /// <summary>Gets the stored name (<c>{name}.v{version}</c>) of an event type.</summary>
    /// <param name="eventType">The event type.</param>
    /// <returns>The stored name.</returns>
    public static string NameOf(Type eventType)
    {
        var contract = ContractOf(eventType);
        return $"{contract.Name}.v{contract.Version.ToString(CultureInfo.InvariantCulture)}";
    }

    /// <summary>Serializes an integration event payload (current version).</summary>
    /// <param name="integrationEvent">The event.</param>
    /// <returns>The JSON payload.</returns>
    public static string Serialize(IIntegrationEvent integrationEvent)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        return JsonSerializer.Serialize(integrationEvent, integrationEvent.GetType(), JsonDefaults.Options);
    }

    /// <summary>Deserializes a stored payload into the current contract type, upcasting older versions.</summary>
    /// <param name="storedName">The stored name (<c>{name}.v{version}</c>, or a legacy CLR name).</param>
    /// <param name="payload">The JSON payload.</param>
    /// <returns>The event.</returns>
    public IIntegrationEvent Deserialize(string storedName, string payload)
    {
        var (contract, storedVersion) = Resolve(storedName);
        var currentVersion = contract.Attribute.Version;
        if (storedVersion > currentVersion)
        {
            throw new InvalidOperationException(
                $"'{storedName}' was written by a newer version (current is v{currentVersion}); it will be retried after the upgrade.");
        }

        if (storedVersion == currentVersion)
        {
            return Materialize(JsonSerializer.Deserialize(payload, contract.Type, JsonDefaults.Options), storedName);
        }

        var node = JsonNode.Parse(payload) as JsonObject
            ?? throw new InvalidOperationException($"Payload of '{storedName}' is not a JSON object.");
        for (var version = storedVersion; version < currentVersion; version++)
        {
            node = _upcasters[(contract.Attribute.Name, version)].Upcast(node);
        }

        return Materialize(node.Deserialize(contract.Type, JsonDefaults.Options), storedName);
    }

    private static IntegrationEventAttribute ContractOf(Type eventType)
    {
        ArgumentNullException.ThrowIfNull(eventType);
        var attribute = eventType.GetCustomAttribute<IntegrationEventAttribute>()
            ?? throw new InvalidOperationException($"{eventType.FullName} must declare [IntegrationEvent(name, version)].");
        if (!NamePattern().IsMatch(attribute.Name) || attribute.Version < 1)
        {
            throw new InvalidOperationException(
                $"{eventType.FullName}: integration event name '{attribute.Name}' must look like 'module.event-name' and the version must be 1 or higher.");
        }

        return attribute;
    }

    private static IIntegrationEvent Materialize(object? value, string storedName)
        => value as IIntegrationEvent ?? throw new InvalidOperationException($"Payload of '{storedName}' deserialized to null.");

    private (Contract Contract, int Version) Resolve(string storedName)
    {
        var match = StoredNamePattern().Match(storedName);
        if (match.Success && _contractsByName.TryGetValue(match.Groups["name"].Value, out var contract))
        {
            return (contract, int.Parse(match.Groups["version"].Value, CultureInfo.InvariantCulture));
        }

        // Rows written before contracts were versioned stored the CLR full name; they are v1.
        if (_contractsByLegacyName.TryGetValue(storedName, out var legacy))
        {
            return (legacy, 1);
        }

        throw new InvalidOperationException($"Unknown integration event '{storedName}'.");
    }

    [GeneratedRegex(@"^[a-z][a-z0-9-]*(\.[a-z][a-z0-9-]*)+$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex NamePattern();

    [GeneratedRegex(@"^(?<name>.+)\.v(?<version>[1-9][0-9]{0,3})$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex StoredNamePattern();

    private sealed record Contract(Type Type, IntegrationEventAttribute Attribute);
}
