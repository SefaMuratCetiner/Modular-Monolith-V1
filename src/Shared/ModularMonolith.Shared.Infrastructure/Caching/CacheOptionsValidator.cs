using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using ModularMonolith.Shared.Infrastructure.Configuration;

namespace ModularMonolith.Shared.Infrastructure.Caching;

/// <summary>Fails startup when the Valkey connection string is missing.</summary>
internal sealed class CacheOptionsValidator(IConfiguration configuration) : IValidateOptions<CacheOptions>
{
    public ValidateOptionsResult Validate(string? name, CacheOptions options)
    {
        var failures = new List<string>();
        if (string.IsNullOrWhiteSpace(options.ConnectionStringName))
        {
            failures.Add("ConnectionStringName is required.");
        }
        else if (string.IsNullOrWhiteSpace(configuration.GetConnectionString(options.ConnectionStringName)))
        {
            failures.Add($"ConnectionStrings:{options.ConnectionStringName} is not configured.");
        }

        return OptionsValidation.ToResult(CacheOptions.SectionName, failures);
    }
}
