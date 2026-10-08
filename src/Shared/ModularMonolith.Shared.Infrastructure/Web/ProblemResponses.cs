using Microsoft.AspNetCore.Http;

namespace ModularMonolith.Shared.Infrastructure.Web;

/// <summary>
/// Writes RFC 9457 problem details from middleware and authentication events (401, 403, 429, 504, …)
/// so every error the API produces has the same shape, including the <c>traceId</c> extension.
/// </summary>
public static class ProblemResponses
{
    /// <summary>Writes a problem details response.</summary>
    /// <param name="context">The HTTP context.</param>
    /// <param name="statusCode">The status code.</param>
    /// <param name="title">A short, human-readable summary of the problem type.</param>
    /// <param name="detail">A human-readable explanation specific to this occurrence.</param>
    /// <returns>A task.</returns>
    public static Task WriteAsync(HttpContext context, int statusCode, string title, string detail)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Response.HasStarted)
        {
            return Task.CompletedTask;
        }

        return Results.Problem(statusCode: statusCode, title: title, detail: detail).ExecuteAsync(context);
    }
}
