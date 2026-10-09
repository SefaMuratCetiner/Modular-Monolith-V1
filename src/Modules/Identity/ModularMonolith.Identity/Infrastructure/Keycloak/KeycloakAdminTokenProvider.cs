using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace ModularMonolith.Identity.Infrastructure.Keycloak;

/// <summary>
/// Gets and caches the service account's access token (OAuth 2.0 client credentials). Singleton: one token
/// is shared until 30 seconds before it expires; concurrent callers wait for a single refresh.
/// </summary>
internal sealed class KeycloakAdminTokenProvider(
    IHttpClientFactory httpClientFactory,
    IOptions<IdentityOptions> options,
    TimeProvider timeProvider) : IDisposable
{
    public const string HttpClientName = "keycloak-admin-token";

    private static readonly TimeSpan ExpirySafetyMargin = TimeSpan.FromSeconds(30);

    private readonly SemaphoreSlim _refresh = new(1, 1);
    private volatile CachedToken? _cached;

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (_cached is { } cached && timeProvider.GetUtcNow() < cached.ExpiresAt)
        {
            return cached.Value;
        }

        await _refresh.WaitAsync(cancellationToken);
        try
        {
            if (_cached is { } current && timeProvider.GetUtcNow() < current.ExpiresAt)
            {
                return current.Value;
            }

            var settings = options.Value;
            using var client = httpClientFactory.CreateClient(HttpClientName);
            using var form = new FormUrlEncodedContent(
            [
                new("grant_type", "client_credentials"),
                new("client_id", settings.AccountErasure.ClientId),
                new("client_secret", settings.AccountErasure.ClientSecret ?? string.Empty),
            ]);
            using var response = await client.PostAsync(new Uri($"{settings.Authority!.TrimEnd('/')}/protocol/openid-connect/token"), form, cancellationToken);
            response.EnsureSuccessStatusCode();

            var token = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken)
                ?? throw new InvalidOperationException("Keycloak returned an empty token response.");
            _cached = new CachedToken(token.AccessToken, timeProvider.GetUtcNow().AddSeconds(token.ExpiresIn) - ExpirySafetyMargin);
            return token.AccessToken;
        }
        finally
        {
            _refresh.Release();
        }
    }

    public void Dispose() => _refresh.Dispose();

    private sealed record CachedToken(string Value, DateTimeOffset ExpiresAt);

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);
}
