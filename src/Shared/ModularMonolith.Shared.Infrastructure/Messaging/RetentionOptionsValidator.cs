using Microsoft.Extensions.Options;
using ModularMonolith.Shared.Infrastructure.Configuration;

namespace ModularMonolith.Shared.Infrastructure.Messaging;

/// <summary>Fails startup on retention settings that could break deduplication or overload the database.</summary>
internal sealed class RetentionOptionsValidator : IValidateOptions<RetentionOptions>
{
    public ValidateOptionsResult Validate(string? name, RetentionOptions options)
    {
        var failures = new List<string>();
        if (options.ProcessedOutboxDays < 0 || options.InboxDays < 0)
        {
            failures.Add("Retention days must be 0 (keep forever) or positive.");
        }

        if (options.InboxDays is > 0 and < 7)
        {
            failures.Add("InboxDays must be 0 or at least 7, so late redeliveries are still deduplicated.");
        }

        if (options.BatchSize is < 100 or > 100_000)
        {
            failures.Add("BatchSize must be between 100 and 100000.");
        }

        var cronFields = options.Cron?.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length ?? 0;
        if (cronFields is not (5 or 6))
        {
            failures.Add("Cron must be a 5- or 6-field cron expression.");
        }

        return OptionsValidation.ToResult(RetentionOptions.SectionName, failures);
    }
}
