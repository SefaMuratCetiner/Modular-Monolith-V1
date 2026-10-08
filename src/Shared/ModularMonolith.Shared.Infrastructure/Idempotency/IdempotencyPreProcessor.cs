using System.Security.Cryptography;
using System.Text.Json;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModularMonolith.Shared.Infrastructure.Observability;
using ModularMonolith.Shared.Infrastructure.Serialization;
using ModularMonolith.Shared.Infrastructure.Web;
using ModularMonolith.Shared.Kernel.Context;
using StackExchange.Redis;

namespace ModularMonolith.Shared.Infrastructure.Idempotency;

/// <summary>
/// FastEndpoints pre-processor implementing the <c>Idempotency-Key</c> header for unsafe requests.
/// Keys are scoped by tenant, user, method and route, so they cannot collide across callers.
/// <list type="bullet">
/// <item>Same key + same payload after completion: the stored response is replayed.</item>
/// <item>Same key while the first request is running: 409 Conflict.</item>
/// <item>Same key with a different payload: 422 Unprocessable Entity.</item>
/// <item>Behaviour without a key or with the store down follows the endpoint's <see cref="IdempotencyPolicy"/>.</item>
/// </list>
/// Pair it with <see cref="IdempotencyPostProcessor{TRequest,TResponse}"/>.
/// Processors are singletons: only singletons are constructor-injected; the scoped tenant and user
/// are read from the request's services.
/// </summary>
/// <typeparam name="TRequest">The endpoint request type.</typeparam>
public sealed class IdempotencyPreProcessor<TRequest>(IdempotencyStore store, ILogger<IdempotencyStore> logger) : IPreProcessor<TRequest>
{
    /// <summary>The request header name.</summary>
    public const string HeaderName = "Idempotency-Key";

    internal const string ItemKey = "__idempotency_claim";

    private const int MaxKeyLength = 100;

    /// <inheritdoc />
    public async Task PreProcessAsync(IPreProcessorContext<TRequest> context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        var http = context.HttpContext;
        if (context.HasValidationFailures || http.Response.HasStarted)
        {
            return;
        }

        var policy = http.GetEndpoint()?.Metadata.GetMetadata<IdempotencyPolicy>() ?? IdempotencyPolicy.Default;
        var key = http.Request.Headers[HeaderName].ToString();
        if (string.IsNullOrWhiteSpace(key))
        {
            if (policy.RequireKey)
            {
                await ProblemResponses.WriteAsync(http, StatusCodes.Status400BadRequest, "The idempotency key is missing.", $"This operation requires an {HeaderName} header.");
            }

            return;
        }

        if (key.Length > MaxKeyLength)
        {
            await ProblemResponses.WriteAsync(http, StatusCodes.Status400BadRequest, "The idempotency key is invalid.", $"The {HeaderName} header must be at most {MaxKeyLength} characters.");
            return;
        }

        var tenant = http.RequestServices.GetRequiredService<ITenantContext>();
        var user = http.RequestServices.GetRequiredService<ICurrentUser>();
        var recordKey = IdempotencyStore.BuildKey(tenant.IsResolved ? tenant.TenantId : "-", user.AuditId, http.Request.Method, http.Request.Path, key);
        var fingerprint = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(context.Request, JsonDefaults.Options)));

        IdempotencyBeginResult begin;
        try
        {
            begin = await store.BeginAsync(recordKey, fingerprint);
        }
        catch (RedisException exception)
        {
            logger.IdempotencyUnavailable(exception);
            if (policy.FailClosed)
            {
                http.Response.Headers.RetryAfter = "5";
                await ProblemResponses.WriteAsync(http, StatusCodes.Status503ServiceUnavailable, "Duplicate protection is unavailable.", "The request was not processed because duplicate protection is temporarily unavailable. Retry with the same key.");
            }

            return;
        }

        await HandleBeginAsync(http, begin, recordKey, fingerprint, ct);
    }

    private static async Task HandleBeginAsync(HttpContext http, IdempotencyBeginResult begin, string recordKey, string fingerprint, CancellationToken ct)
    {
        switch (begin.State)
        {
            case IdempotencyState.Started:
                http.Items[ItemKey] = new IdempotencyClaim(recordKey, fingerprint);
                break;
            case IdempotencyState.Replay:
                http.Response.StatusCode = begin.StatusCode;
                http.Response.ContentType = "application/json";
                http.Response.Headers["Idempotent-Replayed"] = "true";
                await http.Response.WriteAsync(begin.Body ?? string.Empty, ct);
                break;
            case IdempotencyState.InProgress:
                await ProblemResponses.WriteAsync(http, StatusCodes.Status409Conflict, "The request is already being processed.", "A request with this idempotency key is still being processed.");
                break;
            case IdempotencyState.Mismatch:
                await ProblemResponses.WriteAsync(http, StatusCodes.Status422UnprocessableEntity, "The idempotency key was reused.", "The idempotency key was already used with a different request payload.");
                break;
            default:
                throw new InvalidOperationException($"Unknown idempotency state {begin.State}.");
        }
    }
}
