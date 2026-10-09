using FastEndpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using ModularMonolith.Catalog.Domain;
using ModularMonolith.Shared.Infrastructure.Web;
using ModularMonolith.Shared.Kernel.Cqrs;

namespace ModularMonolith.Catalog.Features.GetProduct;

/// <summary>
/// <c>GET /api/v1/catalog/products/{id}</c>: the resource returned in the <c>Location</c> of CreateProduct.
/// Returns an <c>ETag</c>; send it back in <c>If-None-Match</c> (304) or in <c>If-Match</c> when changing the product.
/// </summary>
internal sealed class GetProductEndpoint(IQueryHandler<GetProductQuery, ProductDetails> handler)
    : Endpoint<GetProductRequest, ProductDetails>
{
    /// <summary>Builds the canonical URL of a product.</summary>
    public static string LocationFor(Guid productId) => $"/api/v1/catalog/products/{productId}";

    public override void Configure()
    {
        Get("catalog/products/{id:guid}");
        Version(1);
        Policies(CatalogPermissions.ProductsRead.Name);
        Options(builder => builder.WithTags("Catalog").RequireRateLimiting(RateLimitPolicies.PerUser));
        Description(
            builder => builder
                .Produces<ProductDetails>(StatusCodes.Status200OK)
                .Produces(StatusCodes.Status304NotModified)
                .ProducesProblems(StatusCodes.Status403Forbidden, StatusCodes.Status404NotFound),
            clearDefaults: true);
        Summary(summary => summary.Summary = "Gets a product of the caller's tenant.");
    }

    public override async Task HandleAsync(GetProductRequest req, CancellationToken ct)
    {
        var result = await handler.HandleAsync(new GetProductQuery(new ProductId(req.Id)), ct);
        if (result.IsFailure)
        {
            await Send.ResultAsync(result.Error.ToProblem());
            return;
        }

        // ETag + If-None-Match: unchanged representations cost a 304 without a body.
        var etag = EntityTags.For(result.Value.Version, result.Value);
        HttpContext.Response.Headers.ETag = etag.ToString();
        HttpContext.Response.Headers.CacheControl = "private, no-cache";
        if (EntityTags.IsNotModified(HttpContext.Request, etag))
        {
            await Send.NotModifiedAsync(ct);
            return;
        }

        await Send.OkAsync(result.Value, ct);
    }
}

/// <summary>Route binding (slice-local DTO).</summary>
internal sealed class GetProductRequest
{
    /// <summary>Gets the product id (route).</summary>
    public Guid Id { get; init; }
}
