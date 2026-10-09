using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModularMonolith.Shared.Infrastructure.Modules;
using ModularMonolith.Shared.Infrastructure.Security;

namespace ModularMonolith.Privacy;

/// <summary>
/// Privacy module entry point: data-subject requests. The export combines every module's
/// <c>IPersonalDataExporter</c> (an abstraction in Shared.Kernel), so this module references no other module.
/// Each request is recorded in <c>privacy.data_subject_requests</c> and audited (GDPR accountability).
/// </summary>
public sealed class PrivacyModule : IModule
{
    /// <inheritdoc />
    public string Name => "Privacy";

    /// <inheritdoc />
    public string Schema => "privacy";

    /// <inheritdoc />
    public Assembly Assembly => typeof(PrivacyModule).Assembly;

    /// <inheritdoc />
    /// <remarks>This module publishes no integration events.</remarks>
    public Assembly? ContractsAssembly => null;

    /// <inheritdoc />
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddPermissions(PrivacyPermissions.All);
    }
}
