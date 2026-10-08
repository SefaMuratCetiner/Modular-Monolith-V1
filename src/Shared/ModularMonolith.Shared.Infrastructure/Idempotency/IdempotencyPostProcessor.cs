using System.Text.Json;
using FastEndpoints;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModularMonolith.Shared.Infrastructure.Observability;
using StackExchange.Redis;

namespace ModularMonolith.Shared.Infrastructure.Idempotency;

/// <summary>
/// Completes the idempotency key with the response on 2xx, or releases it on any failure
/// (validation, business error or exception) so the client can safely retry.
/// </summary>
/// <typeparam name="TRequest">The endpoint request type.</typeparam>
/// <typeparam name="TResponse">The endpoint response type.</typeparam>
public sealed class IdempotencyPostProcessor<TRequest, TResponse> : IPostProcessor<TRequest, TResponse>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <inheritdoc />
    public async Task PostProcessAsync(IPostProcessorContext<TRequest, TResponse> context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        var http = context.HttpContext;
        if (!http.Items.TryGetValue(IdempotencyPreProcessor<TRequest>.ItemKey, out var item) || item is not IdempotencyClaim claim)
        {
            return;
        }

        http.Items.Remove(IdempotencyPreProcessor<TRequest>.ItemKey);
        var store = http.RequestServices.GetRequiredService<IdempotencyStore>();
        try
        {
            var status = http.Response.StatusCode;
            if (!context.HasExceptionOccurred && status is >= 200 and < 300 && context.Response is not null)
            {
                await store.CompleteAsync(claim.Key, claim.Fingerprint, status, JsonSerializer.Serialize(context.Response, JsonOptions));
            }
            else
            {
                await store.ReleaseAsync(claim.Key);
            }
        }
        catch (RedisException exception)
        {
            http.RequestServices.GetRequiredService<ILogger<IdempotencyStore>>().IdempotencyUnavailable(exception);
        }
    }
}
