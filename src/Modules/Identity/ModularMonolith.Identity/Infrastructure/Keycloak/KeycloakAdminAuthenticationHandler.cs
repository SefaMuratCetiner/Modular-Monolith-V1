using System.Net.Http.Headers;

namespace ModularMonolith.Identity.Infrastructure.Keycloak;

/// <summary>Adds the service account's bearer token to every admin API request.</summary>
internal sealed class KeycloakAdminAuthenticationHandler(KeycloakAdminTokenProvider tokens) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await tokens.GetAccessTokenAsync(cancellationToken));
        return await base.SendAsync(request, cancellationToken);
    }
}
