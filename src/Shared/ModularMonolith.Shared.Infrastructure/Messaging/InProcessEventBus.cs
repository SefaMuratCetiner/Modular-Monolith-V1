using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModularMonolith.Shared.Infrastructure.Observability;
using ModularMonolith.Shared.Infrastructure.Tenancy;
using ModularMonolith.Shared.Kernel.Messaging;

namespace ModularMonolith.Shared.Infrastructure.Messaging;

/// <summary>
/// In-process integration event bus used by the outbox processor.
/// Every consumer handler runs in its own DI scope, bound to the event's tenant, inside its own
/// inbox-protected transaction. Failures are retried a few times in-process; if any handler still
/// fails, the bus throws so the outbox retries the message later (succeeded handlers are skipped).
/// </summary>
public sealed class InProcessEventBus(
    IServiceScopeFactory scopeFactory,
    IntegrationHandlerMap handlerMap,
    ILogger<InProcessEventBus> logger)
{
    private const int InProcessAttempts = 3;

    private static readonly ConcurrentDictionary<Type, Func<object, IIntegrationEvent, CancellationToken, Task>> Invokers = new();

    private static readonly MethodInfo InvokeMethod =
        typeof(InProcessEventBus).GetMethod(nameof(InvokeAsync), BindingFlags.NonPublic | BindingFlags.Static)!;

    /// <summary>Publishes an integration event to all registered consumers.</summary>
    /// <param name="integrationEvent">The event.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that faults with <see cref="AggregateException"/> when any handler failed.</returns>
    public async Task PublishAsync(IIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        var eventType = integrationEvent.GetType();
        var invoker = Invokers.GetOrAdd(eventType, CreateInvoker);
        var decoratorType = typeof(InboxIntegrationEventDecorator<>).MakeGenericType(eventType);
        var failures = new List<Exception>();

        foreach (var registration in handlerMap.For(eventType))
        {
            using var activity = Telemetry.ActivitySource.StartActivity($"consume {eventType.Name}", ActivityKind.Consumer);
            activity?.SetTag("messaging.message.id", integrationEvent.EventId.ToString());
            activity?.SetTag("modmono.handler", registration.HandlerName);

            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    scope.ServiceProvider.GetRequiredService<TenantContext>().Set(integrationEvent.TenantId);
                    var handler = scope.ServiceProvider.GetRequiredService(registration.HandlerType);
                    var decorated = ActivatorUtilities.CreateInstance(scope.ServiceProvider, decoratorType, handler, registration);
                    await invoker(decorated, integrationEvent, cancellationToken);
                    break;
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    logger.EventHandlerRetry(exception, registration.HandlerName, eventType.Name, attempt);
                    if (attempt >= InProcessAttempts)
                    {
                        activity?.AddException(exception);
                        activity?.SetStatus(ActivityStatusCode.Error);
                        failures.Add(exception);
                        break;
                    }

                    await Task.Delay(TimeSpan.FromMilliseconds(100 * attempt), cancellationToken);
                }
            }
        }

        if (failures.Count > 0)
        {
            throw new AggregateException($"{failures.Count} handler(s) failed for {eventType.Name}.", failures);
        }
    }

    private static Func<object, IIntegrationEvent, CancellationToken, Task> CreateInvoker(Type eventType)
        => InvokeMethod.MakeGenericMethod(eventType).CreateDelegate<Func<object, IIntegrationEvent, CancellationToken, Task>>();

    private static Task InvokeAsync<TEvent>(object handler, IIntegrationEvent integrationEvent, CancellationToken cancellationToken)
        where TEvent : IIntegrationEvent
        => ((IIntegrationEventHandler<TEvent>)handler).HandleAsync((TEvent)integrationEvent, cancellationToken);
}
