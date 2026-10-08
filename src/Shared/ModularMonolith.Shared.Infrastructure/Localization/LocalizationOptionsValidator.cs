using System.Globalization;
using Microsoft.Extensions.Options;
using ModularMonolith.Shared.Infrastructure.Configuration;

namespace ModularMonolith.Shared.Infrastructure.Localization;

/// <summary>Fails startup when no supported culture is configured or a culture name is unknown.</summary>
internal sealed class LocalizationOptionsValidator : IValidateOptions<LocalizationOptions>
{
    public ValidateOptionsResult Validate(string? name, LocalizationOptions options)
    {
        var failures = new List<string>();
        if (options.SupportedCultures.Count == 0)
        {
            failures.Add("SupportedCultures must contain at least one culture.");
        }

        foreach (var culture in options.SupportedCultures)
        {
            try
            {
                _ = CultureInfo.GetCultureInfo(culture, predefinedOnly: true);
            }
            catch (CultureNotFoundException)
            {
                failures.Add($"'{culture}' is not a known culture.");
            }
        }

        return OptionsValidation.ToResult(LocalizationOptions.SectionName, failures);
    }
}
