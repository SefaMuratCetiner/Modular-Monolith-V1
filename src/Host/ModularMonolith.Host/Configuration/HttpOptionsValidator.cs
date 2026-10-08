using Microsoft.Extensions.Options;
using ModularMonolith.Shared.Infrastructure.Configuration;

namespace ModularMonolith.Host.Configuration;

/// <summary>Fails startup on unsafe HTTP hosting limits.</summary>
internal sealed class HttpOptionsValidator : IValidateOptions<HttpOptions>
{
    public ValidateOptionsResult Validate(string? name, HttpOptions options)
    {
        var failures = new List<string>();
        if (options.RequestTimeoutSeconds is < 1 or > 600)
        {
            failures.Add("RequestTimeoutSeconds must be between 1 and 600.");
        }

        if (options.MaxRequestBodyBytes is < 1024 or > 104_857_600)
        {
            failures.Add("MaxRequestBodyBytes must be between 1 KB and 100 MB.");
        }

        if (options.HttpsPort is < 1 or > 65_535)
        {
            failures.Add("HttpsPort must be a valid TCP port.");
        }

        if (options.HstsMaxAgeDays is < 1 or > 730)
        {
            failures.Add("HstsMaxAgeDays must be between 1 and 730.");
        }

        if (options.ShutdownDrainSeconds is < 0 or > 60)
        {
            failures.Add("ShutdownDrainSeconds must be between 0 and 60.");
        }

        if (options.ShutdownTimeoutSeconds is < 5 or > 300)
        {
            failures.Add("ShutdownTimeoutSeconds must be between 5 and 300.");
        }

        return OptionsValidation.ToResult(HttpOptions.SectionName, failures);
    }
}
