using FastEndpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using ModularMonolith.Shared.Infrastructure.Web;
using ModularMonolith.Shared.Kernel.Cqrs;
using ModularMonolith.Shared.Kernel.Paging;

namespace ModularMonolith.Catalog.Features.ListProducts;

/// <summary><c>GET /api/v1/catalog/products?page=1&amp;pageSize=20</c>.</summary>
internal sealed class ListProductsEndpoint(IQueryHandler<ListProductsQuery, PagedResult<ProductListItem>> handler)
    : Endpoint<PagingRequest, PagedResult<ProductListItem>>
{
    public override void Configure()
    {
        Get("catalog/products");
        Version(1);
        Policies(CatalogPermissions.ProductsRead.Name);
        Options(builder => builder.WithTags("Catalog").RequireRateLimiting(RateLimitPolicies.PerUser));
        Description(
            builder => builder
                .Produces<PagedResult<ProductListItem>>(StatusCodes.Status200OK)
                .ProducesProblems(StatusCodes.Status400BadRequest, StatusCodes.Status403Forbidden),
            clearDefaults: true);
        Summary(summary => summary.Summary = "Lists one page of the caller's tenant's products.");
    }

    public override async Task HandleAsync(PagingRequest req, CancellationToken ct)
    {
        var paging = PageRequest.Create(req.Page, req.PageSize);
        if (paging.IsFailure)
        {
            await Send.ResultAsync(paging.Error.ToProblem());
            return;
        }

        var result = await handler.HandleAsync(new ListProductsQuery(paging.Value), ct);
        if (result.IsFailure)
        {
            await Send.ResultAsync(result.Error.ToProblem());
            return;
        }

        await Send.OkAsync(result.Value, ct);
    }
}
