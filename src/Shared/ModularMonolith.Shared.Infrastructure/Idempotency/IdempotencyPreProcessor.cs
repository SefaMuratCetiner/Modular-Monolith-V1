using System.Security.Cryptography;
using System.Text.Json;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModularMonolith.Shared.Infrastructure.Observability;
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
/// </list>
/// Pair it with <see cref="IdempotencyPostProcessor{TRequest,TResponse}"/>.
/// </summary>
/// <typeparam name="TRequest">The endpoint request type.</typeparam>
public sealed class IdempotencyPreProcessor<TRequest> : IPreProcessor<TRequest>
{
    /// <summary>The request header name.</summary>
    public const string HeaderName = "Idempotency-Key";

    internal const string ItemKey = "__idempotency_claim";

    private const int MaxKeyLength = 100;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <inheritdoc />
    public async Task PreProcessAsync(IPreProcessorContext<TRequest> context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        var http = context.HttpContext;
        if (context.HasValidationFailures || http.Response.HasStarted)
        {
            return;
        }

        var key = http.Request.Headers[HeaderName].ToString();
        if (string.IsNullOrWhiteSpace(key))
        {
            return; // The header is optional; without it the request is processed normally.
        }

        if (key.Length > MaxKeyLength)
        {
            await WriteProblemAsync(http, StatusCodes.Status400BadRequest, $"The {HeaderName} header must be at most {MaxKeyLength} characters.");
            return;
        }

        var services = http.RequestServices;
        var tenant = services.GetRequiredService<ITenantContext>();
        var user = services.GetRequiredService<ICurrentUser>();
        var scopedKey = $"idempotency:{(tenant.IsResolved ? tenant.TenantId : "-")}:{user.AuditId}:{http.Request.Method}:{http.Request.Path}:{key}";
        var fingerprint = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(context.Request, JsonOptions)));

        IdempotencyBeginResult begin;
        try
        {
            begin = await services.GetRequiredService<IdempotencyStore>().BeginAsync(scopedKey, fingerprint);
        }
        catch (RedisException exception)
        {
            services.GetRequiredService<ILogger<IdempotencyStore>>().IdempotencyUnavailable(exception);
            return;
        }

        switch (begin.State)
        {
            case IdempotencyState.Started:
                http.Items[ItemKey] = new IdempotencyClaim(scopedKey, fingerprint);
                break;
            case IdempotencyState.Replay:
                http.Response.StatusCode = begin.StatusCode;
                http.Response.ContentType = "application/json";
                http.Response.Headers["Idempotent-Replayed"] = "true";
                await http.Response.WriteAsync(begin.Body ?? string.Empty, ct);
                break;
            case IdempotencyState.InProgress:
                await WriteProblemAsync(http, StatusCodes.Status409Conflict, "A request with this idempotency key is still being processed.");
                break;
            case IdempotencyState.Mismatch:
                await WriteProblemAsync(http, StatusCodes.Status422UnprocessableEntity, "The idempotency key was already used with a different request payload.");
                break;
            default:
                throw new InvalidOperationException($"Unknown idempotency state {begin.State}.");
        }
    }

    private static Task WriteProblemAsync(HttpContext http, int statusCode, string detail)
        => Results.Problem(statusCode: statusCode, title: "Idempotency", detail: detail).ExecuteAsync(http);
}
