using Microsoft.AspNetCore.Http;
using ModularMonolith.Shared.Kernel.Results;

namespace ModularMonolith.Shared.Infrastructure.Web;

/// <summary>
/// Maps <see cref="Error"/> values to RFC 9457 problem details responses.
/// <c>title</c> is a human-readable summary of the problem type, <c>detail</c> describes this
/// occurrence, and the stable machine-readable error code is in the <c>code</c> extension.
/// </summary>
public static class ResultHttpExtensions
{
    /// <summary>The problem-details extension carrying the stable error code.</summary>
    public const string CodeExtension = "code";

    /// <summary>Converts an error into a problem details <see cref="IResult"/>.</summary>
    /// <param name="error">The error.</param>
    /// <returns>The HTTP result.</returns>
    public static IResult ToProblem(this Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        var extensions = new Dictionary<string, object?>(StringComparer.Ordinal) { [CodeExtension] = error.Code };
        if (error is ValidationError validation)
        {
            return TypedResults.ValidationProblem(
                validation.Errors,
                detail: error.Message,
                title: TitleFor(error.Type),
                extensions: extensions);
        }

        return TypedResults.Problem(
            title: TitleFor(error.Type),
            detail: error.Message,
            statusCode: StatusCodeFor(error.Type),
            extensions: extensions);
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

    /// <summary>Maps an error category to a human-readable problem title.</summary>
    /// <param name="type">The error type.</param>
    /// <returns>The title.</returns>
    public static string TitleFor(ErrorType type) => type switch
    {
        ErrorType.Validation => "One or more validation errors occurred.",
        ErrorType.NotFound => "The resource was not found.",
        ErrorType.Conflict => "The request conflicts with the current state of the resource.",
        ErrorType.Forbidden => "The operation is not allowed.",
        ErrorType.Unavailable => "The operation is temporarily unavailable.",
        _ => "An unexpected error occurred.",
    };
}
