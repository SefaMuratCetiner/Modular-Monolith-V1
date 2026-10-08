using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ModularMonolith.Shared.Infrastructure.Modules;

/// <summary>
/// The single public entry point of a module. The host only knows modules through this contract;
/// everything else inside a module is <c>internal</c>.
/// </summary>
public interface IModule
{
    /// <summary>Gets the module display name, e.g. <c>Catalog</c>.</summary>
    string Name { get; }

    /// <summary>Gets the PostgreSQL schema owned exclusively by this module, e.g. <c>catalog</c>.</summary>
    string Schema { get; }

    /// <summary>Gets the implementation assembly (endpoints, handlers, validators, migrations).</summary>
    Assembly Assembly { get; }

    /// <summary>Gets the public contracts assembly (integration events other modules may consume).</summary>
    Assembly ContractsAssembly { get; }

    /// <summary>Registers module-specific services (options, repositories, HTTP clients).</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    void AddServices(IServiceCollection services, IConfiguration configuration);
}
