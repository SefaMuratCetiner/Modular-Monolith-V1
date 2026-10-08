using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using ModularMonolith.Shared.Infrastructure.Data;
using ModularMonolith.Shared.Kernel.Cqrs;
using ModularMonolith.Shared.Kernel.Domain;

namespace ModularMonolith.Shared.Infrastructure.Cqrs;

/// <summary>
/// Dispatches domain events of tracked aggregates to in-module handlers, inside the current transaction.
/// Uses cached generic delegates instead of <c>dynamic</c> dispatch.
/// </summary>
internal sealed class DomainEventDispatcher(IServiceProvider services)
{
    private const int MaxRounds = 10;

    private static readonly ConcurrentDictionary<Type, Func<IServiceProvider, IDomainEvent, CancellationToken, Task>> Invokers = new();

    private static readonly MethodInfo InvokeMethod =
        typeof(DomainEventDispatcher).GetMethod(nameof(InvokeHandlersAsync), BindingFlags.NonPublic | BindingFlags.Static)!;

    public async Task DispatchAsync(DbSession session, CancellationToken cancellationToken)
    {
        // Handlers may change other aggregates that raise further events; dispatch until quiet.
        for (var round = 0; round < MaxRounds; round++)
        {
            IHasDomainEvents[] aggregates = [.. session.TrackedAggregates];
            IDomainEvent[] events = [.. aggregates.SelectMany(aggregate => aggregate.DomainEvents)];
            if (events.Length == 0)
            {
                return;
            }

            foreach (var aggregate in aggregates)
            {
                aggregate.ClearDomainEvents();
            }

            foreach (var domainEvent in events)
            {
                var invoker = Invokers.GetOrAdd(domainEvent.GetType(), CreateInvoker);
                await invoker(services, domainEvent, cancellationToken);
            }
        }

        throw new InvalidOperationException($"Domain event dispatch did not complete within {MaxRounds} rounds.");
    }

    private static Func<IServiceProvider, IDomainEvent, CancellationToken, Task> CreateInvoker(Type eventType)
        => InvokeMethod.MakeGenericMethod(eventType).CreateDelegate<Func<IServiceProvider, IDomainEvent, CancellationToken, Task>>();

    private static async Task InvokeHandlersAsync<TEvent>(IServiceProvider services, IDomainEvent domainEvent, CancellationToken cancellationToken)
        where TEvent : IDomainEvent
    {
        foreach (var handler in services.GetServices<IDomainEventHandler<TEvent>>())
        {
            await handler.HandleAsync((TEvent)domainEvent, cancellationToken);
        }
    }
}
