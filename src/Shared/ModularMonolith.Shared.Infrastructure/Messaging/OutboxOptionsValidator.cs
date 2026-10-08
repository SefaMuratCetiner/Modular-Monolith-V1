using Microsoft.Extensions.Options;
using ModularMonolith.Shared.Infrastructure.Configuration;

namespace ModularMonolith.Shared.Infrastructure.Messaging;

/// <summary>Fails startup on outbox settings that would stall or overload delivery.</summary>
internal sealed class OutboxOptionsValidator : IValidateOptions<OutboxOptions>
{
    public ValidateOptionsResult Validate(string? name, OutboxOptions options)
    {
        var failures = new List<string>();
        if (options.BatchSize is < 1 or > 1000)
        {
            failures.Add("BatchSize must be between 1 and 1000.");
        }

        if (options.MaxBatchesPerRun is < 1 or > 1000)
        {
            failures.Add("MaxBatchesPerRun must be between 1 and 1000.");
        }

        if (options.MaxAttempts is < 1 or > 100)
        {
            failures.Add("MaxAttempts must be between 1 and 100.");
        }

        if (options.BaseDelaySeconds < 1)
        {
            failures.Add("BaseDelaySeconds must be at least 1.");
        }

        if (options.MaxDelaySeconds < options.BaseDelaySeconds)
        {
            failures.Add("MaxDelaySeconds must be greater than or equal to BaseDelaySeconds.");
        }

        if (options.LeaseSeconds is < 10 or > 3600)
        {
            failures.Add("LeaseSeconds must be between 10 and 3600.");
        }

        if (options.MetricsIntervalSeconds is < 5 or > 3600)
        {
            failures.Add("MetricsIntervalSeconds must be between 5 and 3600.");
        }

        var cronFields = options.SweepCron?.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length ?? 0;
        if (cronFields is not (5 or 6))
        {
            failures.Add("SweepCron must be a 5- or 6-field cron expression.");
        }

        return OptionsValidation.ToResult(OutboxOptions.SectionName, failures);
    }
}
