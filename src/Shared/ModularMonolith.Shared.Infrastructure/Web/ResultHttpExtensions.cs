using Microsoft.AspNetCore.Http;
using ModularMonolith.Shared.Kernel.Results;

namespace ModularMonolith.Shared.Infrastructure.Web;

/// <summary>
/// Maps <see cref="Error"/> values to RFC 9457 problem details responses.
/// </summary>
public static class ResultHttpExtensions
{
    /// <summary>Converts an error into a problem details <see cref="IResult"/>.</summary>
    /// <param name="error">The error.</param>
    /// <returns>The HTTP result.</returns>
    public static IResult ToProblem(this Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        if (error is ValidationError validation)
        {
            return TypedResults.ValidationProblem(
                validation.Errors,
                title: "Validation failed",
                extensions: new Dictionary<string, object?>(StringComparer.Ordinal) { ["code"] = error.Code });
        }

        var status = StatusCodeFor(error.Type);
        return TypedResults.Problem(
            title: error.Code,
            detail: error.Message,
            statusCode: status,
            extensions: new Dictionary<string, object?>(StringComparer.Ordinal) { ["code"] = error.Code });
    }

    /// <summary>Maps an error category to an HTTP status code.</summary>
    /// <param name="type">The error type.</param>
    /// <returns>The status code.</returns>
    public static int StatusCodeFor(ErrorType type) => type switch
    {
        ErrorType.Validation => StatusCodes.Status400BadRequest,
        ErrorType.NotFound => StatusCodes.Status404NotFound,
        ErrorType.Conflict => StatusCodes.Status409Conflict,
        ErrorType.Forbidden => StatusCodes.Status403Forbidden,
        ErrorType.Unavailable => StatusCodes.Status503ServiceUnavailable,
        _ => StatusCodes.Status500InternalServerError,
    };
}
