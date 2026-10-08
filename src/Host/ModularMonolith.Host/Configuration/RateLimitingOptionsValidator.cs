using Microsoft.Extensions.Options;
using ModularMonolith.Shared.Infrastructure.Configuration;

namespace ModularMonolith.Host.Configuration;

/// <summary>Fails startup on unusable rate limits.</summary>
internal sealed class RateLimitingOptionsValidator : IValidateOptions<RateLimitingOptions>
{
    public ValidateOptionsResult Validate(string? name, RateLimitingOptions options)
    {
        var failures = new List<string>();
        if (options.PermitLimit is < 1 or > 100_000)
        {
            failures.Add("PermitLimit must be between 1 and 100000.");
        }

        if (options.WindowSeconds is < 1 or > 3600)
        {
            failures.Add("WindowSeconds must be between 1 and 3600.");
        }

        return OptionsValidation.ToResult(RateLimitingOptions.SectionName, failures);
    }
}
