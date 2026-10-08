using Microsoft.Extensions.Options;
using ModularMonolith.Shared.Infrastructure.Configuration;

namespace ModularMonolith.Identity;

/// <summary>Fails startup when the identity provider authority is missing or malformed.</summary>
internal sealed class IdentityOptionsValidator : IValidateOptions<IdentityOptions>
{
    public ValidateOptionsResult Validate(string? name, IdentityOptions options)
    {
        string[] failures = OptionsValidation.IsHttpUrl(options.Authority)
            ? []
            : ["Authority (or Authentication:Authority) must be an absolute http(s) URL."];
        return OptionsValidation.ToResult(IdentityOptions.SectionName, failures);
    }
}
