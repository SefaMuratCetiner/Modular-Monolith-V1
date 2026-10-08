using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using ModularMonolith.Shared.Infrastructure.Configuration;
using ModularMonolith.Shared.Infrastructure.Data;

namespace ModularMonolith.Shared.Infrastructure.Migrations;

/// <summary>Fails the migration run early on missing or unsafe settings.</summary>
internal sealed class MigrationOptionsValidator(IConfiguration configuration, IOptions<DatabaseOptions> database) : IValidateOptions<MigrationOptions>
{
    public ValidateOptionsResult Validate(string? name, MigrationOptions options)
    {
        var failures = new List<string>();
        if (string.IsNullOrWhiteSpace(options.ConnectionStringName))
        {
            failures.Add("ConnectionStringName is required.");
        }
        else if (string.IsNullOrWhiteSpace(configuration.GetConnectionString(options.ConnectionStringName)))
        {
            failures.Add($"ConnectionStrings:{options.ConnectionStringName} (schema owner) is not configured.");
        }

        if (!SqlIdentifier.IsValid(database.Value.ApplicationRole))
        {
            failures.Add("Database:ApplicationRole must be a lowercase SQL identifier.");
        }

        if (options.ApplicationLogin is not null && !SqlIdentifier.IsValid(options.ApplicationLogin))
        {
            failures.Add("ApplicationLogin must be a lowercase SQL identifier.");
        }

        if (!string.IsNullOrEmpty(options.ApplicationLoginPassword) && options.ApplicationLogin is null)
        {
            failures.Add("ApplicationLoginPassword requires ApplicationLogin.");
        }

        return OptionsValidation.ToResult(MigrationOptions.SectionName, failures);
    }
}
