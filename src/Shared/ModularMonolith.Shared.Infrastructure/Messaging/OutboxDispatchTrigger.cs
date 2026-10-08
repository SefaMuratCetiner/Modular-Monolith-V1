using Hangfire;
using Microsoft.Extensions.Logging;
using ModularMonolith.Shared.Infrastructure.Observability;

namespace ModularMonolith.Shared.Infrastructure.Messaging;

/// <summary>
/// Enqueues an immediate outbox run after a commit so events are delivered in milliseconds
/// instead of waiting for the recurring sweep.
/// </summary>
internal sealed class OutboxDispatchTrigger(IBackgroundJobClient jobs, ILogger<OutboxDispatchTrigger> logger)
{
    public void Trigger(string schema)
    {
        try
        {
            jobs.Enqueue<OutboxProcessor>(processor => processor.ProcessAsync(schema, CancellationToken.None));
        }
#pragma warning disable CA1031 // Best effort: the recurring sweep guarantees eventual delivery.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            logger.OutboxTriggerFailed(exception, schema);
        }
    }
}
