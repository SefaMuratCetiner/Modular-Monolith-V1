using System.Diagnostics;
using FastEndpoints;
using FluentValidation.Results;
using ModularMonolith.Shared.Infrastructure.Serialization;
using ModularMonolith.Shared.Infrastructure.Web;

namespace ModularMonolith.Host.Configuration;

/// <summary>
/// FastEndpoints conventions: URL versioning (<c>/api/v{n}/…</c>), the shared JSON settings and
/// binding/validation errors in the same RFC 9457 shape as <c>Result</c> validation errors.
/// </summary>
internal static class ApiConventions
{
    private const string ValidationTitle = "One or more validation errors occurred.";

    public static void Apply(Config config)
    {
        config.Endpoints.RoutePrefix = "api";
        config.Versioning.Prefix = "v";
        config.Versioning.PrependToRoute = true;
        config.Versioning.DefaultVersion = 1;

        JsonDefaults.Apply(config.Serializer.Options);

        config.Errors.ContentType = "application/problem+json";
        config.Errors.ProducesMetadataType = typeof(HttpValidationProblemDetails);
        config.Errors.ResponseBuilder = BuildValidationProblem;
    }

    private static HttpValidationProblemDetails BuildValidationProblem(List<ValidationFailure> failures, HttpContext context, int statusCode)
    {
        var errors = failures
            .GroupBy(failure => string.IsNullOrEmpty(failure.PropertyName) ? "request" : failure.PropertyName, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Select(failure => failure.ErrorMessage).ToArray(), StringComparer.Ordinal);

        var problem = new HttpValidationProblemDetails(errors)
        {
            Status = statusCode,
            Title = ValidationTitle,
            Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
            Instance = context.Request.Path,
        };
        problem.Extensions[ResultHttpExtensions.CodeExtension] = "validation_failed";
        problem.Extensions["traceId"] = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;
        return problem;
    }
}
