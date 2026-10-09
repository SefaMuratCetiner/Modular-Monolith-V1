using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModularMonolith.Shared.Infrastructure.Modules;
using ModularMonolith.Shared.Infrastructure.Security;

namespace ModularMonolith.Messaging;

/// <summary>
/// Messaging module entry point: operator tooling for message delivery between modules. Today: the dead-letter
/// queue (dead-lettered outbox messages of every module, across tenants); the natural home for further
/// messaging features (outbox backlog views, a message broker relay). It uses the outbox's administration API
/// from Shared.Infrastructure and never touches another module's business tables. Every replay is an audited
/// command.
/// </summary>
public sealed class MessagingModule : IModule
{
    /// <inheritdoc />
    public string Name => "Messaging";

    /// <inheritdoc />
    public string Schema => "messaging";

    /// <inheritdoc />
    public Assembly Assembly => typeof(MessagingModule).Assembly;

    /// <inheritdoc />
    /// <remarks>This module publishes no integration events.</remarks>
    public Assembly? ContractsAssembly => null;

    /// <inheritdoc />
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddPermissions(MessagingPermissions.All);
    }
}
