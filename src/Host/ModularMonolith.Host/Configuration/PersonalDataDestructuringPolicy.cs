using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using ModularMonolith.Shared.Infrastructure.Privacy;
using Serilog.Core;
using Serilog.Events;

namespace ModularMonolith.Host.Configuration;

/// <summary>
/// Serilog destructuring policy: when an object with <c>[PersonalData]</c> properties is logged with
/// <c>{@Value}</c>, those properties are written as <c>[redacted]</c>. Types without marked properties
/// fall through to Serilog's default destructuring.
/// </summary>
internal sealed class PersonalDataDestructuringPolicy : IDestructuringPolicy
{
    private static readonly ConcurrentDictionary<Type, (PropertyInfo Property, bool IsPersonal)[]> PropertyCache = new();

    public bool TryDestructure(object value, ILogEventPropertyValueFactory propertyValueFactory, [NotNullWhen(true)] out LogEventPropertyValue? result)
    {
        var type = value.GetType();
        if (!PersonalDataRedaction.HasPersonalData(type))
        {
            result = null;
            return false;
        }

        var properties = PropertyCache.GetOrAdd(type, static candidate =>
        [
            .. candidate.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(property => property.CanRead && property.GetIndexParameters().Length == 0)
                .Select(property => (property, PersonalDataRedaction.IsPersonalData(property))),
        ]);

        result = new StructureValue(
            properties.Select(entry => new LogEventProperty(
                entry.Property.Name,
                entry.IsPersonal
                    ? new ScalarValue(PersonalDataRedaction.Redacted)
                    : propertyValueFactory.CreatePropertyValue(entry.Property.GetValue(value), destructureObjects: true))),
            type.Name);
        return true;
    }
}
