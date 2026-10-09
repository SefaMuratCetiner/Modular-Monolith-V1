using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace ModularMonolith.Shared.Infrastructure.Security;

/// <summary>Registers permissions as authorization policies (one policy per permission, same name).</summary>
public static partial class PermissionExtensions
{
    /// <summary>
    /// Registers permissions. Every permission becomes an authorization policy with the same name, so
    /// endpoints use <c>Policies(CatalogPermissions.ProductsWrite.Name)</c>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="permissions">The permissions owned by the caller (module or host).</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddPermissions(this IServiceCollection services, params Permission[] permissions)
    {
        ArgumentNullException.ThrowIfNull(permissions);
        foreach (var permission in permissions)
        {
            services.AddSingleton(permission);
        }

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IAuthorizationHandler, PermissionAuthorizationHandler>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IConfigureOptions<AuthorizationOptions>, PermissionPolicySetup>());
        return services;
    }

    [GeneratedRegex("^[a-z][a-z0-9-]*(\\.[a-z][a-z0-9-]*)+$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    internal static partial Regex NamePattern();

    /// <summary>Validates all permissions once and adds their policies (fails startup on a bad definition).</summary>
    private sealed class PermissionPolicySetup(IEnumerable<Permission> permissions) : IConfigureOptions<AuthorizationOptions>
    {
        public void Configure(AuthorizationOptions options)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var permission in permissions)
            {
                if (!NamePattern().IsMatch(permission.Name) || !NamePattern().IsMatch(permission.Scope) || permission.Roles.Count == 0)
                {
                    throw new InvalidOperationException(
                        $"Permission '{permission.Name}' is invalid: name and scope must be lowercase dot-separated identifiers and at least one role is required.");
                }

                if (!seen.Add(permission.Name))
                {
                    throw new InvalidOperationException($"Permission '{permission.Name}' is registered twice.");
                }

                options.AddPolicy(permission.Name, policy => policy
                    .RequireAuthenticatedUser()
                    .AddRequirements(new PermissionRequirement(permission)));
            }
        }
    }
}
