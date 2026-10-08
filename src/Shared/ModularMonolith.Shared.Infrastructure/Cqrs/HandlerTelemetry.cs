using System.Diagnostics;
using ModularMonolith.Shared.Infrastructure.Observability;
using ModularMonolith.Shared.Kernel.Results;

namespace ModularMonolith.Shared.Infrastructure.Cqrs;

/// <summary>Shared tracing/metrics logic for command and query decorators.</summary>
internal static class HandlerTelemetry
{
    public static async Task<Result<TResponse>> TraceAsync<TResponse>(string kind, string name, Func<Task<Result<TResponse>>> handle)
    {
        using var activity = Telemetry.ActivitySource.StartActivity($"{kind} {name}", ActivityKind.Internal);
        activity?.SetTag("modmono.request.kind", kind);
        activity?.SetTag("modmono.request.name", name);
        var started = Stopwatch.GetTimestamp();
        var outcome = "exception";
        try
        {
            var result = await handle();
            outcome = result.IsSuccess ? "success" : result.Error.Code;
            if (result.IsFailure)
            {
                activity?.SetStatus(ActivityStatusCode.Error, result.Error.Code);
            }

            return result;
        }
        catch (Exception exception)
        {
            activity?.AddException(exception);
            activity?.SetStatus(ActivityStatusCode.Error, exception.GetType().Name);
            throw;
        }
        finally
        {
            Telemetry.RequestDuration.Record(
                Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                new KeyValuePair<string, object?>("kind", kind),
                new KeyValuePair<string, object?>("name", name),
                new KeyValuePair<string, object?>("outcome", outcome));
        }
    }
}
