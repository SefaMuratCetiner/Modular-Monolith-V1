using System.Collections.Frozen;
using System.Text.RegularExpressions;
using ModularMonolith.Shared.Infrastructure.Privacy;
using Serilog.Core;
using Serilog.Events;

namespace ModularMonolith.Host.Configuration;

/// <summary>
/// Last line of defence for log events (every sink): masks personal data that reached a log property by
/// accident, e.g. a query string or a third-party library's message.
/// <list type="bullet">
/// <item>Properties with a sensitive name (<c>Email</c>, <c>DisplayName</c>, <c>Password</c>, tokens, ...)
/// at any depth become <c>[redacted]</c>.</item>
/// <item>E-mail addresses inside any string value become <c>[redacted-email]</c>.</item>
/// </list>
/// The primary control is not logging personal data at all (log ids, never names) and <c>[PersonalData]</c>
/// with <see cref="PersonalDataDestructuringPolicy"/>. Exception messages are not rewritten; Npgsql omits
/// parameter values and row details from them unless <c>Include Error Detail=true</c>, which must stay off.
/// </summary>
internal sealed partial class PersonalDataMaskingEnricher : ILogEventEnricher
{
    private const string RedactedEmail = "[redacted-email]";

    private static readonly ScalarValue RedactedValue = new(PersonalDataRedaction.Redacted);

    private static readonly FrozenSet<string> SensitiveNames = FrozenSet.Create(
        StringComparer.OrdinalIgnoreCase,
        "email",
        "emailAddress",
        "displayName",
        "firstName",
        "lastName",
        "phone",
        "phoneNumber",
        "password",
        "accessToken",
        "refreshToken",
        "idToken",
        "authorization");

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        List<LogEventProperty>? replacements = null;
        foreach (var (name, value) in logEvent.Properties)
        {
            var masked = Mask(name, value);
            if (!ReferenceEquals(masked, value))
            {
                (replacements ??= []).Add(new LogEventProperty(name, masked));
            }
        }

        foreach (var replacement in replacements ?? [])
        {
            logEvent.AddOrUpdateProperty(replacement);
        }
    }

    private static LogEventPropertyValue Mask(string name, LogEventPropertyValue value)
    {
        if (SensitiveNames.Contains(name))
        {
            return RedactedValue;
        }

        return value switch
        {
            ScalarValue { Value: string text } when EmailPattern().IsMatch(text) => new ScalarValue(EmailPattern().Replace(text, RedactedEmail)),
            StructureValue structure => MaskStructure(structure),
            SequenceValue sequence => MaskSequence(sequence),
            DictionaryValue dictionary => MaskDictionary(dictionary),
            _ => value,
        };
    }

    private static StructureValue MaskStructure(StructureValue structure)
    {
        var properties = structure.Properties.Select(property => (property, masked: Mask(property.Name, property.Value))).ToList();
        return properties.TrueForAll(entry => ReferenceEquals(entry.masked, entry.property.Value))
            ? structure
            : new StructureValue(properties.Select(entry => new LogEventProperty(entry.property.Name, entry.masked)), structure.TypeTag);
    }

    private static SequenceValue MaskSequence(SequenceValue sequence)
    {
        var elements = sequence.Elements.Select(element => (element, masked: Mask(string.Empty, element))).ToList();
        return elements.TrueForAll(entry => ReferenceEquals(entry.masked, entry.element))
            ? sequence
            : new SequenceValue(elements.Select(entry => entry.masked));
    }

    private static DictionaryValue MaskDictionary(DictionaryValue dictionary)
    {
        var elements = dictionary.Elements
            .Select(entry => (entry.Key, entry.Value, masked: Mask(entry.Key.Value as string ?? string.Empty, entry.Value)))
            .ToList();
        return elements.TrueForAll(entry => ReferenceEquals(entry.masked, entry.Value))
            ? dictionary
            : new DictionaryValue(elements.Select(entry => new KeyValuePair<ScalarValue, LogEventPropertyValue>(entry.Key, entry.masked)));
    }

    [GeneratedRegex(@"[A-Za-z0-9._%+-]+@[A-Za-z0-9-]+(\.[A-Za-z0-9-]+)*\.[A-Za-z]{2,}", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 100)]
    private static partial Regex EmailPattern();
}
