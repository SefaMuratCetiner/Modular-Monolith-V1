using FastEndpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using ModularMonolith.Shared.Infrastructure.Web;
using RequeueDeadLettersHandler = ModularMonolith.Shared.Kernel.Cqrs.ICommandHandler<
    ModularMonolith.Messaging.Features.RequeueDeadLetters.RequeueDeadLettersCommand, int>;

namespace ModularMonolith.Messaging.Features.RequeueDeadLetters;

/// <summary>
/// <c>POST /api/v1/messaging/{module}/dead-letters/requeue</c> (all) and
/// <c>POST /api/v1/messaging/{module}/dead-letters/{id}/requeue</c> (one), platform operators only.
/// </summary>
internal sealed class RequeueDeadLettersEndpoint(RequeueDeadLettersHandler handler)
    : Endpoint<RequeueDeadLettersRequest, RequeueDeadLettersResponse>
{
    public override void Configure()
    {
        Post("messaging/{module}/dead-letters/requeue", "messaging/{module}/dead-letters/{id:guid}/requeue");
        Version(1);
        Policies(MessagingPermissions.DeadLettersReplay.Name);
        Options(builder => builder.WithTags("Messaging").RequireRateLimiting(RateLimitPolicies.PerUser));
        Description(
            builder => builder
                .Produces<RequeueDeadLettersResponse>(StatusCodes.Status200OK)
                .ProducesProblems(StatusCodes.Status403Forbidden, StatusCodes.Status404NotFound),
            clearDefaults: true);
        Summary(summary => summary.Summary = "Re-queues one or all dead-lettered outbox messages of a module (operators only).");
    }

    public override async Task HandleAsync(RequeueDeadLettersRequest req, CancellationToken ct)
    {
        var result = await handler.HandleAsync(new RequeueDeadLettersCommand(req.Module, req.Id), ct);
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
