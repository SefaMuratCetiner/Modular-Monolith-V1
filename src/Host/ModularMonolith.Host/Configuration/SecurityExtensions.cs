using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using ModularMonolith.Shared.Infrastructure.Web;

namespace ModularMonolith.Host.Configuration;

/// <summary>
/// Authentication, authorization and rate limiting.
/// <list type="bullet">
/// <item><c>/api/*</c> (or any request with a Bearer token): JWT bearer, audience-validated, 401 on failure.</item>
/// <item>Everything else (e.g. <c>/hangfire</c>): cookie session with OpenID Connect login (PKCE, public client).</item>
/// <item>Authentication is required by default (fallback policy); health and metrics opt out explicitly.</item>
/// </list>
/// </summary>
internal static class SecurityExtensions
{
    public const string AdminPolicy = "admin";

    private const string SelectorScheme = "jwt-or-cookie";

    public static WebApplicationBuilder AddSecurity(this WebApplicationBuilder builder)
    {
        AddAuthenticationSchemes(builder);

        builder.Services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AddPolicy(AdminPolicy, policy => policy.RequireAuthenticatedUser().RequireRole("admin"));

        AddRateLimiting(builder);
        return builder;
    }

    private static void AddAuthenticationSchemes(WebApplicationBuilder builder)
    {
        var settings = builder.Configuration.GetSection("Authentication");
        var authority = settings["Authority"];
        var requireHttpsMetadata = !builder.Environment.IsDevelopment();

        builder.Services
            .AddAuthentication(options =>
            {
                options.DefaultScheme = SelectorScheme;
                options.DefaultChallengeScheme = SelectorScheme;
            })
            .AddPolicyScheme(SelectorScheme, "JWT for APIs, cookie for browsers", options =>
                options.ForwardDefaultSelector = context =>
                    context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase)
                    || context.Request.Headers.Authorization.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                        ? JwtBearerDefaults.AuthenticationScheme
                        : CookieAuthenticationDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = authority;
                options.Audience = settings["Audience"] ?? "modular-monolith-api";
                options.RequireHttpsMetadata = requireHttpsMetadata;
                options.MapInboundClaims = false;
                options.TokenValidationParameters.NameClaimType = "preferred_username";
                options.TokenValidationParameters.RoleClaimType = "roles";
            })
            .AddCookie(options =>
            {
                options.Cookie.Name = "mm.session";
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                options.ForwardChallenge = OpenIdConnectDefaults.AuthenticationScheme;
            })
            .AddOpenIdConnect(options =>
            {
                options.Authority = authority;
                options.ClientId = settings["WebClientId"] ?? "modular-monolith-web";
                options.ResponseType = "code";
                options.UsePkce = true;
                options.SaveTokens = false;
                options.MapInboundClaims = false;
                options.GetClaimsFromUserInfoEndpoint = true;
                options.RequireHttpsMetadata = requireHttpsMetadata;
                options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.Scope.Add("profile");
                options.Scope.Add("email");
                options.TokenValidationParameters.NameClaimType = "preferred_username";
                options.TokenValidationParameters.RoleClaimType = "roles";
            });
    }

    private static void AddRateLimiting(WebApplicationBuilder builder)
    {
        var permitLimit = builder.Configuration.GetValue("RateLimiting:PermitLimit", 100);
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(RateLimitPolicies.PerUser, context =>
            {
                // One bucket per tenant + user, so a noisy caller cannot starve everyone else.
                var tenant = context.User.FindFirst("tenant_id")?.Value ?? "-";
                var caller = context.User.FindFirst("sub")?.Value
                    ?? context.Connection.RemoteIpAddress?.ToString()
                    ?? "anonymous";
                return RateLimitPartition.GetFixedWindowLimiter($"{tenant}:{caller}", _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permitLimit,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                });
            });
        });
    }
}
