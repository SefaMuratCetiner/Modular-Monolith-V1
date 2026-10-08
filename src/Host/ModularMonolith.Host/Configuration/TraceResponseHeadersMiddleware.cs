using System.Diagnostics;

namespace ModularMonolith.Host.Configuration;

/// <summary>
/// Returns the request's trace to the caller, so a support ticket or client log can be matched to the
/// server-side trace and logs:
/// <list type="bullet">
/// <item><c>traceresponse</c>: W3C Trace Context Level 2 format (<c>00-{trace-id}-{span-id}-{flags}</c>).</item>
/// <item><c>X-Trace-Id</c>: just the trace id, for humans and simple clients.</item>
/// </list>
/// Problem details bodies carry the same id in their <c>traceId</c> extension.
/// </summary>
internal sealed class TraceResponseHeadersMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context)
    {
        var activity = Activity.Current;
        if (activity is not null)
        {
            context.Response.OnStarting(() =>
            {
                var flags = activity.Recorded ? "01" : "00";
                context.Response.Headers["traceresponse"] = $"00-{activity.TraceId}-{activity.SpanId}-{flags}";
                context.Response.Headers["X-Trace-Id"] = activity.TraceId.ToString();
                return Task.CompletedTask;
            });
        }

        return next(context);
    }
}
