using Microsoft.Extensions.Options;
using ModularMonolith.Shared.Infrastructure.Configuration;

namespace ModularMonolith.Shared.Infrastructure.Data;

/// <summary>Fails startup when the database settings or connection string are missing or unsafe.</summary>
internal sealed class DatabaseOptionsValidator : IValidateOptions<DatabaseOptions>
{
    public ValidateOptionsResult Validate(string? name, DatabaseOptions options)
    {
        var failures = new List<string>();
        if (string.IsNullOrWhiteSpace(options.ConnectionStringName))
        {
            failures.Add("ConnectionStringName is required.");
        }
        else if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            failures.Add($"ConnectionStrings:{options.ConnectionStringName} is not configured.");
        }

        if (!SqlIdentifier.IsValid(options.ApplicationRole))
        {
            failures.Add("ApplicationRole must be a lowercase SQL identifier.");
        }

        if (options.SlowQueryThresholdMs is < 0 or > 60_000)
        {
            failures.Add("SlowQueryThresholdMs must be between 0 (disabled) and 60000.");
        }

        if (options.TransientRetries is < 0 or > 5)
        {
            failures.Add("TransientRetries must be between 0 and 5.");
        }

        return OptionsValidation.ToResult(DatabaseOptions.SectionName, failures);
    }
}
