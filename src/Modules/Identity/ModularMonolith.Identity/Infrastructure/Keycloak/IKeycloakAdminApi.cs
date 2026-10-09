using Refit;

namespace ModularMonolith.Identity.Infrastructure.Keycloak;

/// <summary>
/// The parts of Keycloak's admin REST API (<c>/admin/realms/{realm}/...</c>) Identity uses. Base address:
/// the Keycloak server root. Requests carry a service-account token (<see cref="KeycloakAdminAuthenticationHandler"/>).
/// The Keycloak user id equals the token's <c>sub</c> claim.
/// </summary>
internal interface IKeycloakAdminApi
{
    [Delete("/admin/realms/{realm}/users/{userId}")]
    Task<IApiResponse> DeleteUserAsync(string realm, string userId, CancellationToken cancellationToken);

    [Put("/admin/realms/{realm}/users/{userId}")]
    Task<IApiResponse> UpdateUserAsync(string realm, string userId, [Body] KeycloakUserUpdate update, CancellationToken cancellationToken);

    [Post("/admin/realms/{realm}/users/{userId}/logout")]
    Task<IApiResponse> LogoutAsync(string realm, string userId, CancellationToken cancellationToken);
}

/// <summary>Partial user representation for <c>PUT /users/{id}</c> (only the fields sent are changed).</summary>
/// <param name="Enabled">Whether the account can sign in.</param>
internal sealed record KeycloakUserUpdate(bool Enabled);
