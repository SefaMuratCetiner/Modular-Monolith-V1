using Microsoft.Extensions.Options;
using ModularMonolith.Shared.Infrastructure.Configuration;

namespace ModularMonolith.Host.Configuration;

/// <summary>Fails startup when the identity provider is not configured correctly.</summary>
internal sealed class IdentityProviderOptionsValidator : IValidateOptions<IdentityProviderOptions>
{
    public ValidateOptionsResult Validate(string? name, IdentityProviderOptions options)
    {
        var failures = new List<string>();
        if (!OptionsValidation.IsHttpUrl(options.Authority, requireHttps: options.RequireHttpsMetadata))
        {
            failures.Add(options.RequireHttpsMetadata
                ? "Authority must be an absolute https URL (set RequireHttpsMetadata=false only for local development)."
                : "Authority must be an absolute http(s) URL.");
        }

        if (string.IsNullOrWhiteSpace(options.Audience))
        {
            failures.Add("Audience is required.");
        }

        if (string.IsNullOrWhiteSpace(options.WebClientId))
        {
            failures.Add("WebClientId is required.");
        }

        return OptionsValidation.ToResult(IdentityProviderOptions.SectionName, failures);
    }
}
