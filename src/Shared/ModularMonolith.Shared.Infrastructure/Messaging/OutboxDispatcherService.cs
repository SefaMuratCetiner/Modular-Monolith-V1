using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModularMonolith.Shared.Infrastructure.Observability;

namespace ModularMonolith.Shared.Infrastructure.Messaging;

/// <summary>
/// Background worker that drains a module's outbox as soon as <see cref="OutboxSignal"/> fires,
/// so events are delivered milliseconds after commit. Runs are sequential (bounded concurrency:
/// background work cannot starve request processing).
/// </summary>
internal sealed class OutboxDispatcherService(
    OutboxSignal signal,
    IServiceScopeFactory scopeFactory,
    ILogger<OutboxDispatcherService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            string schema;
            try
            {
                schema = await signal.WaitAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<OutboxProcessor>().ProcessAsync(schema, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
#pragma warning disable CA1031 // The worker must survive any failure; the recurring sweep retries.
            catch (Exception exception)
#pragma warning restore CA1031
            {
                logger.OutboxDispatchFailed(exception, schema);
            }
        }
    }
}
