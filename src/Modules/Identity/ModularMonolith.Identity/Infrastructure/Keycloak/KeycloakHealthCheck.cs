using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ModularMonolith.Identity.Infrastructure.Keycloak;

/// <summary>
/// Readiness check: the identity provider publishes its discovery document. Reported as degraded
/// because already-issued tokens keep validating with cached signing keys.
/// </summary>
internal sealed class KeycloakHealthCheck(IKeycloakMetadataApi api) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await api.GetDiscoveryDocumentAsync(cancellationToken);
            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Degraded($"Keycloak discovery returned {(int)response.StatusCode}.");
        }
        catch (HttpRequestException exception)
        {
            return HealthCheckResult.Degraded("Keycloak is unreachable.", exception);
        }
    }
}
