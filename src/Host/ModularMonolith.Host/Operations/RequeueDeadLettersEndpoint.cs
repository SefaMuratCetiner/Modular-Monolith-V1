using FastEndpoints;
using ModularMonolith.Host.Configuration;
using ModularMonolith.Shared.Infrastructure.Messaging;
using ModularMonolith.Shared.Infrastructure.Web;

namespace ModularMonolith.Host.Operations;

/// <summary>
/// <c>POST /api/v1/ops/outbox/{module}/dead-letters/requeue</c> (all) and
/// <c>POST /api/v1/ops/outbox/{module}/dead-letters/{id}/requeue</c> (one), platform operators only.
/// Replays dead-lettered messages after the cause is fixed; consumers are inbox-protected, so
/// handlers that already succeeded are not run again.
/// </summary>
internal sealed class RequeueDeadLettersEndpoint(OutboxAdministration administration)
    : Endpoint<RequeueDeadLettersRequest, RequeueDeadLettersResponse>
{
    public override void Configure()
    {
        Post("ops/outbox/{module}/dead-letters/requeue", "ops/outbox/{module}/dead-letters/{id:guid}/requeue");
        Version(1);
        Policies(PlatformPermissions.OutboxReplay.Name);
        Options(builder => builder.WithTags("Operations").RequireRateLimiting(RateLimitPolicies.PerUser));
        Description(
            builder => builder
                .Produces<RequeueDeadLettersResponse>(StatusCodes.Status200OK)
                .ProducesProblems(StatusCodes.Status403Forbidden, StatusCodes.Status404NotFound),
            clearDefaults: true);
        Summary(summary => summary.Summary = "Re-queues one or all dead-lettered outbox messages of a module (operators only).");
    }

    public override async Task HandleAsync(RequeueDeadLettersRequest req, CancellationToken ct)
    {
        var result = await administration.RequeueAsync(req.Module, req.Id, ct);
        if (result.IsFailure)
        {
            await Send.ResultAsync(result.Error.ToProblem());
            return;
        }

        await Send.OkAsync(new RequeueDeadLettersResponse(result.Value), ct);
    }
}

/// <summary>Route values.</summary>
internal sealed class RequeueDeadLettersRequest
{
    /// <summary>Gets the module schema.</summary>
    public string Module { get; init; } = string.Empty;

    /// <summary>Gets the message id, or <see langword="null"/> to re-queue every dead letter.</summary>
    public Guid? Id { get; init; }
}

/// <summary>How many messages were re-queued.</summary>
internal sealed record RequeueDeadLettersResponse(int Requeued);
