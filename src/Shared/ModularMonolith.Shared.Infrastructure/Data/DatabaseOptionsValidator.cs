using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using ModularMonolith.Shared.Infrastructure.Configuration;

namespace ModularMonolith.Shared.Infrastructure.Data;

/// <summary>Fails startup when the database settings or connection string are missing or unsafe.</summary>
internal sealed class DatabaseOptionsValidator(IConfiguration configuration) : IValidateOptions<DatabaseOptions>
{
    public ValidateOptionsResult Validate(string? name, DatabaseOptions options)
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

        if (!string.IsNullOrEmpty(options.ApplicationRole) && !SqlIdentifier.IsValid(options.ApplicationRole))
        {
            failures.Add("ApplicationRole must be a lowercase SQL identifier (or empty).");
        }

        return OptionsValidation.ToResult(DatabaseOptions.SectionName, failures);
    }
}
