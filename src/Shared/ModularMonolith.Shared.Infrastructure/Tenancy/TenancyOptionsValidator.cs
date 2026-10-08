using Microsoft.Extensions.Options;
using ModularMonolith.Shared.Infrastructure.Configuration;

namespace ModularMonolith.Shared.Infrastructure.Tenancy;

/// <summary>Fails startup on unusable tenancy settings.</summary>
internal sealed class TenancyOptionsValidator : IValidateOptions<TenancyOptions>
{
    public ValidateOptionsResult Validate(string? name, TenancyOptions options)
    {
        var failures = new List<string>();
        if (string.IsNullOrWhiteSpace(options.ClaimType))
        {
            failures.Add("ClaimType is required.");
        }

        if (options.AllowDefaultTenant && !TenantContext.IsValid(options.DefaultTenant))
        {
            failures.Add("DefaultTenant must be a valid tenant id when AllowDefaultTenant is true.");
        }

        if (!string.IsNullOrEmpty(options.RootDomain)
            && Uri.CheckHostName(options.RootDomain) is not (UriHostNameType.Dns or UriHostNameType.Basic))
        {
            failures.Add("RootDomain must be a host name such as 'example.com' (no scheme or path).");
        }

        return OptionsValidation.ToResult(TenancyOptions.SectionName, failures);
    }
}
