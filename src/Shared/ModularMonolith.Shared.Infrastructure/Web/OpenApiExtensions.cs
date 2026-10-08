using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace ModularMonolith.Shared.Infrastructure.Web;

/// <summary>
/// OpenAPI response metadata helpers so every endpoint documents its problem-details responses.
/// </summary>
public static class OpenApiExtensions
{
    /// <summary>
    /// Documents the problem-details responses an endpoint can return. 400 is documented as a
    /// validation problem; 401 and 429 are added automatically because every API endpoint
    /// requires authentication and is rate limited.
    /// </summary>
    /// <param name="builder">The route handler builder.</param>
    /// <param name="statusCodes">Additional endpoint-specific status codes (e.g. 404, 409).</param>
    /// <returns>The builder.</returns>
    public static RouteHandlerBuilder ProducesProblems(this RouteHandlerBuilder builder, params int[] statusCodes)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(statusCodes);
        int[] codes = [.. statusCodes.Append(StatusCodes.Status401Unauthorized).Append(StatusCodes.Status429TooManyRequests).Distinct().Order()];
        foreach (var code in codes)
        {
            if (code == StatusCodes.Status400BadRequest)
            {
                builder.ProducesValidationProblem();
            }
            else
            {
                builder.ProducesProblem(code);
            }
        }

        return builder;
    }
}
