using Refit;

namespace ModularMonolith.Identity.Infrastructure.Keycloak;

/// <summary>Refit client for the realm's OpenID Connect discovery document.</summary>
internal interface IKeycloakMetadataApi
{
    [Get("/.well-known/openid-configuration")]
    Task<HttpResponseMessage> GetDiscoveryDocumentAsync(CancellationToken cancellationToken);
}
