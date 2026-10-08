namespace ModularMonolith.Shared.Infrastructure.Messaging;

/// <summary>
/// Startup-built map of integration event type to consumer handlers (and the consumer's schema,
/// which hosts its inbox table).
/// </summary>
public sealed class IntegrationHandlerMap
{
    private readonly Dictionary<Type, List<IntegrationHandlerRegistration>> _handlers = [];

    /// <summary>Gets the handlers for an event type.</summary>
    /// <param name="eventType">The event type.</param>
    /// <returns>The handler registrations (possibly empty).</returns>
    public IReadOnlyList<IntegrationHandlerRegistration> For(Type eventType)
        => _handlers.TryGetValue(eventType, out var list) ? list : [];

    internal void Add(Type eventType, Type handlerType, string consumerSchema)
    {
        if (!_handlers.TryGetValue(eventType, out var list))
        {
            list = [];
            _handlers[eventType] = list;
        }

        list.Add(new IntegrationHandlerRegistration(handlerType, consumerSchema));
    }
}

/// <summary>A consumer handler registration.</summary>
/// <param name="HandlerType">The concrete handler type.</param>
/// <param name="ConsumerSchema">The consuming module's schema (location of its inbox table).</param>
public sealed record IntegrationHandlerRegistration(Type HandlerType, string ConsumerSchema)
{
    /// <summary>Gets the stable handler name recorded in the inbox.</summary>
    public string HandlerName => HandlerType.FullName ?? HandlerType.Name;
}
