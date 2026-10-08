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
/// Every consumer handler runs in its own DI scope, bound to the event's tenant (logs and spans are
/// tagged with it), inside its own inbox-protected transaction. Consumer spans are children of the
/// producer's trace. Failures are retried a few times in-process; if any handler still fails, the
/// bus throws so the outbox retries the message later (succeeded handlers are skipped by the inbox).
/// </summary>
public sealed class InProcessEventBus(
    IServiceScopeFactory scopeFactory,
    IntegrationHandlerMap handlerMap,
    ILogger<InProcessEventBus> logger)
{
    private const int InProcessAttempts = 3;

    private static readonly MethodInfo InvokeMethod =
        typeof(InProcessEventBus).GetMethod(nameof(InvokeAsync), BindingFlags.NonPublic | BindingFlags.Static)!;

    // Built once per event type: no reflection or constructor lookup per message.
    private static readonly ConcurrentDictionary<Type, EventPipeline> Pipelines = new();

    /// <summary>Publishes an integration event to all registered consumers.</summary>
    /// <param name="integrationEvent">The event.</param>
    /// <param name="traceParent">The producer's W3C <c>traceparent</c>, if any.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that faults with <see cref="AggregateException"/> when any handler failed.</returns>
    public async Task PublishAsync(IIntegrationEvent integrationEvent, string? traceParent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        var eventType = integrationEvent.GetType();
        var pipeline = Pipelines.GetOrAdd(eventType, EventPipeline.Create);
        var parentContext = ActivityContext.TryParse(traceParent, traceState: null, isRemote: true, out var producer)
            ? producer
            : default;
        var failures = new List<Exception>();

        foreach (var registration in handlerMap.For(eventType))
        {
            using var activity = Telemetry.ActivitySource.StartActivity($"consume {eventType.Name}", ActivityKind.Consumer, parentContext);
            activity?.SetTag("messaging.message.id", integrationEvent.EventId.ToString());
            activity?.SetTag("modmono.handler", registration.HandlerName);
            activity?.SetTag(TenantTelemetry.TagName, integrationEvent.TenantId);

            using (logger.BeginScope(TenantTelemetry.LogScope(integrationEvent.TenantId)))
            {
                var failure = await InvokeWithRetriesAsync(pipeline, registration, integrationEvent, cancellationToken);
                if (failure is not null)
                {
                    activity?.AddException(failure);
                    activity?.SetStatus(ActivityStatusCode.Error);
                    failures.Add(failure);
                }
            }
        }

        if (failures.Count > 0)
        {
            throw new AggregateException($"{failures.Count} handler(s) failed for {eventType.Name}.", failures);
        }
    }

    private async Task<Exception?> InvokeWithRetriesAsync(
        EventPipeline pipeline,
        IntegrationHandlerRegistration registration,
        IIntegrationEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                scope.ServiceProvider.GetRequiredService<TenantContext>().Set(integrationEvent.TenantId);
                var handler = scope.ServiceProvider.GetRequiredService(registration.HandlerType);
                var decorated = pipeline.CreateDecorator(scope.ServiceProvider, [handler, registration]);
                await pipeline.Invoke(decorated, integrationEvent, cancellationToken);
                return null;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.EventHandlerRetry(exception, registration.HandlerName, integrationEvent.GetType().Name, attempt);
                if (attempt >= InProcessAttempts)
                {
                    return exception;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(100 * attempt), cancellationToken);
            }
        }
    }

    private static Task InvokeAsync<TEvent>(object handler, IIntegrationEvent integrationEvent, CancellationToken cancellationToken)
        where TEvent : IIntegrationEvent
        => ((IIntegrationEventHandler<TEvent>)handler).HandleAsync((TEvent)integrationEvent, cancellationToken);

    /// <summary>Cached, per-event-type activation and invocation delegates.</summary>
    private sealed record EventPipeline(ObjectFactory CreateDecorator, Func<object, IIntegrationEvent, CancellationToken, Task> Invoke)
    {
        public static EventPipeline Create(Type eventType)
        {
            var handlerInterface = typeof(IIntegrationEventHandler<>).MakeGenericType(eventType);
            var decoratorType = typeof(InboxIntegrationEventDecorator<>).MakeGenericType(eventType);
            return new EventPipeline(
                ActivatorUtilities.CreateFactory(decoratorType, [handlerInterface, typeof(IntegrationHandlerRegistration)]),
                InvokeMethod.MakeGenericMethod(eventType).CreateDelegate<Func<object, IIntegrationEvent, CancellationToken, Task>>());
        }
    }
}
