using Microsoft.Extensions.Options;
using ModularMonolith.Shared.Infrastructure.Configuration;

namespace ModularMonolith.Identity;

/// <summary>Fails startup when the identity provider authority or the account-erasure client is misconfigured.</summary>
internal sealed class IdentityOptionsValidator : IValidateOptions<IdentityOptions>
{
    public ValidateOptionsResult Validate(string? name, IdentityOptions options)
    {
        var failures = new List<string>();
        if (!OptionsValidation.IsHttpUrl(options.Authority))
        {
            failures.Add("Authority (or Authentication:Authority) must be an absolute http(s) URL.");
        }

        var erasure = options.AccountErasure;
        if (erasure.Enabled)
        {
            if (options.Realm is null)
            {
                failures.Add("Authority must have the form https://<host>/realms/<realm> to erase Keycloak accounts.");
            }

            if (string.IsNullOrWhiteSpace(erasure.ClientId) || string.IsNullOrWhiteSpace(erasure.ClientSecret))
            {
                failures.Add("AccountErasure:ClientId and AccountErasure:ClientSecret are required (secret store), or set AccountErasure:Enabled=false.");
            }

            if (!Enum.IsDefined(erasure.Mode))
            {
                failures.Add("AccountErasure:Mode must be Delete or Disable.");
            }
        }

        return OptionsValidation.ToResult(IdentityOptions.SectionName, failures);
    }
}
