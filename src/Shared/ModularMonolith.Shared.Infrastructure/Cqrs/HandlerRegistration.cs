using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ModularMonolith.Shared.Infrastructure.Messaging;
using ModularMonolith.Shared.Infrastructure.Modules;
using ModularMonolith.Shared.Kernel.Cqrs;
using ModularMonolith.Shared.Kernel.Messaging;

namespace ModularMonolith.Shared.Infrastructure.Cqrs;

/// <summary>
/// Scans a module assembly and registers its handlers with their decorator chains.
/// No mediator: endpoints inject <c>ICommandHandler&lt;TCommand, TResponse&gt;</c> directly.
/// </summary>
internal static class HandlerRegistration
{
    // Innermost first. Effective call order: Logging -> Telemetry -> Validation -> Transaction -> handler.
    private static readonly Type[] CommandDecorators =
    [
        typeof(TransactionalCommandDecorator<,>),
        typeof(ValidationCommandDecorator<,>),
        typeof(TelemetryCommandDecorator<,>),
        typeof(LoggingCommandDecorator<,>),
    ];

    // Innermost first. Effective call order: Logging -> Telemetry -> handler.
    private static readonly Type[] QueryDecorators =
    [
        typeof(TelemetryQueryDecorator<,>),
        typeof(LoggingQueryDecorator<,>),
    ];

    public static void AddModuleHandlers(this IServiceCollection services, IModule module, IntegrationHandlerMap integrationHandlers)
    {
        var candidates = module.Assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false });

        foreach (var type in candidates)
        {
            foreach (var contract in type.GetInterfaces().Where(contract => contract.IsGenericType))
            {
                var definition = contract.GetGenericTypeDefinition();
                if (definition == typeof(ICommandHandler<,>))
                {
                    RegisterDecorated(services, contract, type, CommandDecorators);
                }
                else if (definition == typeof(IQueryHandler<,>))
                {
                    RegisterDecorated(services, contract, type, QueryDecorators);
                }
                else if (definition == typeof(IDomainEventHandler<>))
                {
                    services.AddScoped(contract, type);
                }
                else if (definition == typeof(IIntegrationEventHandler<>))
                {
                    // Not registered under the interface: the event bus resolves the concrete type
                    // in a fresh, tenant-bound scope and wraps it with the inbox decorator.
                    services.TryAddScoped(type);
                    integrationHandlers.Add(contract.GetGenericArguments()[0], type, module.Schema);
                }
            }
        }
    }

    private static void RegisterDecorated(IServiceCollection services, Type serviceType, Type implementationType, Type[] decorators)
    {
        services.TryAddScoped(implementationType);
        var arguments = serviceType.GetGenericArguments();

        // Constructor lookup happens once here; each resolve only invokes the compiled factories.
        ObjectFactory[] factories =
        [
            .. decorators.Select(decorator => ActivatorUtilities.CreateFactory(decorator.MakeGenericType(arguments), [serviceType])),
        ];

        services.AddScoped(serviceType, provider =>
        {
            var instance = provider.GetRequiredService(implementationType);
            foreach (var factory in factories)
            {
                instance = factory(provider, [instance]);
            }

            return instance;
        });
    }
}
