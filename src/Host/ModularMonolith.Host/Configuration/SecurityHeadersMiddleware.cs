namespace ModularMonolith.Host.Configuration;

/// <summary>
/// Adds browser security headers to every response. JSON API responses get a locked-down CSP;
/// the HTML UIs (Hangfire dashboard, Swagger UI) get a policy that allows their own inline assets.
/// </summary>
internal sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    private const string ApiContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'";

    private const string UiContentSecurityPolicy =
        "default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'; " +
        "img-src 'self' data:; font-src 'self' data:; connect-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";

    public Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = "DENY";
            headers["Referrer-Policy"] = "no-referrer";
            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";
            headers["Cross-Origin-Opener-Policy"] = "same-origin";
            headers.ContentSecurityPolicy = IsHtmlUi(context.Request.Path) ? UiContentSecurityPolicy : ApiContentSecurityPolicy;
            return Task.CompletedTask;
        });

        return next(context);
    }

    private static bool IsHtmlUi(PathString path)
        => path.StartsWithSegments("/hangfire", StringComparison.OrdinalIgnoreCase)
           || path.StartsWithSegments("/swagger", StringComparison.OrdinalIgnoreCase);
}
