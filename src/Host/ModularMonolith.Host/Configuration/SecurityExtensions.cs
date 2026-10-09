using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using ModularMonolith.Shared.Infrastructure.Configuration;
using ModularMonolith.Shared.Infrastructure.Security;
using ModularMonolith.Shared.Infrastructure.Web;

namespace ModularMonolith.Host.Configuration;

/// <summary>
/// Authentication, authorization and rate limiting.
/// <list type="bullet">
/// <item><c>/api/*</c> (or any request with a Bearer token): JWT bearer, audience-validated, 401 problem details on failure.</item>
/// <item>Everything else (e.g. <c>/hangfire</c>): cookie session with OpenID Connect login (PKCE, public client).</item>
/// <item>Authentication is required by default (fallback policy); health and metrics opt out explicitly.</item>
/// <item>API endpoints require a <see cref="Permission"/>: a realm role plus an OAuth scope in the token.
/// Modules register their own permissions; the host registers <see cref="PlatformPermissions"/>.</item>
/// <item>401, 403 and 429 responses carry RFC 9457 problem details; 429 also carries <c>Retry-After</c>.</item>
/// </list>
/// All settings come from validated options, so a missing authority stops the host at startup.
/// </summary>
internal static class SecurityExtensions
{
    /// <summary>
    /// Platform operators (role <c>operator</c>) signed in through the browser: the Hangfire dashboard.
    /// Cookie sessions carry no OAuth scopes, so this is role-based; API endpoints use permissions.
    /// </summary>
    public const string OperatorPolicy = "platform-operator";

    private const string SelectorScheme = "jwt-or-cookie";

    public static WebApplicationBuilder AddSecurity(this WebApplicationBuilder builder)
    {
        builder.Services.AddValidatedOptions<IdentityProviderOptions, IdentityProviderOptionsValidator>(builder.Configuration, IdentityProviderOptions.SectionName);
        builder.Services.AddValidatedOptions<RateLimitingOptions, RateLimitingOptionsValidator>(builder.Configuration, RateLimitingOptions.SectionName);

        AddAuthenticationSchemes(builder.Services, builder.Environment.IsDevelopment());

        builder.Services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AddPolicy(OperatorPolicy, policy => policy.RequireAuthenticatedUser().RequireRole(AppRoles.Operator));

        builder.Services.AddPermissions(PlatformPermissions.All);

        AddRateLimiting(builder.Services);
        return builder;
    }

    private static void AddAuthenticationSchemes(IServiceCollection services, bool isDevelopment)
    {
        services
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
            .AddJwtBearer()
            .AddCookie(options =>
            {
                options.Cookie.Name = "mm.session";
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = isDevelopment ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
                options.ForwardChallenge = OpenIdConnectDefaults.AuthenticationScheme;
            })
            .AddOpenIdConnect();

        ConfigureJwtBearer(services);
        ConfigureOpenIdConnect(services);
    }

    private static void ConfigureJwtBearer(IServiceCollection services)
    {
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<IdentityProviderOptions>>((options, identityProvider) =>
            {
                var settings = identityProvider.Value;
                options.Authority = settings.Authority;
                options.Audience = settings.Audience;
                options.RequireHttpsMetadata = settings.RequireHttpsMetadata;
                options.MapInboundClaims = false;
                options.TokenValidationParameters.NameClaimType = ClaimNames.PreferredUserName;
                options.TokenValidationParameters.RoleClaimType = ClaimNames.Roles;
                options.Events = new JwtBearerEvents
                {
                    OnChallenge = WriteChallengeAsync,
                    OnForbidden = context => ProblemResponses.WriteAsync(
                        context.HttpContext,
                        StatusCodes.Status403Forbidden,
                        "The operation is not allowed.",
                        "The authenticated caller lacks the permission required for this operation."),
                };
            });
    }

    private static void ConfigureOpenIdConnect(IServiceCollection services)
    {
        services.AddOptions<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme)
            .Configure<IOptions<IdentityProviderOptions>>((options, identityProvider) =>
            {
                var settings = identityProvider.Value;
                options.Authority = settings.Authority;
                options.ClientId = settings.WebClientId;
                options.RequireHttpsMetadata = settings.RequireHttpsMetadata;
                options.ResponseType = "code";
                options.UsePkce = true;
                options.SaveTokens = false;
                options.MapInboundClaims = false;
                options.GetClaimsFromUserInfoEndpoint = true;
                options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.Scope.Add("profile");
                options.Scope.Add("email");
                options.TokenValidationParameters.NameClaimType = ClaimNames.PreferredUserName;
                options.TokenValidationParameters.RoleClaimType = ClaimNames.Roles;
            });
    }

    private static Task WriteChallengeAsync(JwtBearerChallengeContext context)
    {
        // Replace the empty default 401 with problem details; keep the RFC 6750 WWW-Authenticate header.
        context.HandleResponse();
        var invalidToken = context.AuthenticateFailure is not null;
        context.Response.Headers.WWWAuthenticate = invalidToken ? "Bearer error=\"invalid_token\"" : "Bearer";
        return ProblemResponses.WriteAsync(
            context.HttpContext,
            StatusCodes.Status401Unauthorized,
            "Authentication is required.",
            invalidToken ? "The bearer access token is invalid or expired." : "A bearer access token is required.");
    }

    private static void AddRateLimiting(IServiceCollection services)
    {
        services.AddRateLimiter(_ => { });
        services.AddOptions<RateLimiterOptions>()
            .Configure<IOptions<RateLimitingOptions>>((options, rateLimiting) =>
            {
                var limits = rateLimiting.Value;
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.OnRejected = OnRejectedAsync;
                options.AddPolicy(RateLimitPolicies.PerUser, context =>
                {
                    // One bucket per tenant + user, so a noisy caller cannot starve everyone else.
                    var tenant = context.User.FindFirst(ClaimNames.TenantId)?.Value ?? "-";
                    var caller = context.User.FindFirst(ClaimNames.Subject)?.Value
                        ?? context.Connection.RemoteIpAddress?.ToString()
                        ?? "anonymous";
                    return RateLimitPartition.GetFixedWindowLimiter($"{tenant}:{caller}", _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = limits.PermitLimit,
                        Window = TimeSpan.FromSeconds(limits.WindowSeconds),
                        QueueLimit = 0,
                    });
                });
            });
    }

    private static async ValueTask OnRejectedAsync(OnRejectedContext context, CancellationToken cancellationToken)
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter =
                ((long)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
        }

        await ProblemResponses.WriteAsync(
            context.HttpContext,
            StatusCodes.Status429TooManyRequests,
            "Too many requests.",
            "The rate limit for this caller was exceeded. Retry after the number of seconds in the Retry-After header.");
    }
}
