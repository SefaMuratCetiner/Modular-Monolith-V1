using FastEndpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using ModularMonolith.Shared.Infrastructure.Web;
using ModularMonolith.Shared.Kernel.Cqrs;

namespace ModularMonolith.Catalog.Features.ListProducts;

/// <summary><c>GET /api/v1/catalog/products</c>.</summary>
internal sealed class ListProductsEndpoint(IQueryHandler<ListProductsQuery, IReadOnlyList<ProductListItem>> handler)
    : EndpointWithoutRequest<IReadOnlyList<ProductListItem>>
{
    public override void Configure()
    {
        Get("catalog/products");
        Version(1);
        Options(builder => builder.WithTags("Catalog").RequireRateLimiting(RateLimitPolicies.PerUser));
        Summary(summary => summary.Summary = "Lists the products of the caller's tenant.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = await handler.HandleAsync(new ListProductsQuery(), ct);
        if (result.IsFailure)
        {
            await Send.ResultAsync(result.Error.ToProblem());
            return;
        }

        await Send.OkAsync(result.Value, ct);
    }
}
