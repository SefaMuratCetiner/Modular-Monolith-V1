using FastEndpoints;
using ModularMonolith.Host.Configuration;
using ModularMonolith.Shared.Infrastructure.Messaging;
using ModularMonolith.Shared.Infrastructure.Web;
using ModularMonolith.Shared.Kernel.Paging;

namespace ModularMonolith.Host.Operations;

/// <summary>
/// <c>GET /api/v1/ops/outbox/{module}/dead-letters?page=1&amp;pageSize=20</c> (platform operators only).
/// Lists dead-lettered outbox messages of a module across all tenants; payloads are not exposed.
/// </summary>
internal sealed class ListDeadLettersEndpoint(OutboxAdministration administration)
    : Endpoint<ListDeadLettersRequest, PagedResult<DeadLetter>>
{
    public override void Configure()
    {
        Get("ops/outbox/{module}/dead-letters");
        Version(1);
        Policies(PlatformPermissions.OutboxRead.Name);
        Options(builder => builder.WithTags("Operations").RequireRateLimiting(RateLimitPolicies.PerUser));
        Description(
            builder => builder
                .Produces<PagedResult<DeadLetter>>(StatusCodes.Status200OK)
                .ProducesProblems(StatusCodes.Status400BadRequest, StatusCodes.Status403Forbidden, StatusCodes.Status404NotFound),
            clearDefaults: true);
        Summary(summary => summary.Summary = "Lists dead-lettered outbox messages of a module (operators only).");
    }

    public override async Task HandleAsync(ListDeadLettersRequest req, CancellationToken ct)
    {
        var paging = PageRequest.Create(req.Page, req.PageSize);
        if (paging.IsFailure)
        {
            await Send.ResultAsync(paging.Error.ToProblem());
            return;
        }

        var result = await administration.ListDeadLettersAsync(req.Module, paging.Value, ct);
        if (result.IsFailure)
        {
            await Send.ResultAsync(result.Error.ToProblem());
            return;
        }

        await Send.OkAsync(result.Value, ct);
    }
}

/// <summary>Route (<c>module</c> = module schema, e.g. <c>catalog</c>) and paging.</summary>
internal sealed class ListDeadLettersRequest
{
    /// <summary>Gets the module schema.</summary>
    public string Module { get; init; } = string.Empty;

    /// <summary>Gets the 1-based page number.</summary>
    [QueryParam]
    public int Page { get; init; } = 1;

    /// <summary>Gets the page size.</summary>
    [QueryParam]
    public int PageSize { get; init; } = PageRequest.DefaultPageSize;
}
