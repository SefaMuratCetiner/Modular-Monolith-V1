using FastEndpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using ModularMonolith.Shared.Infrastructure.Web;
using ModularMonolith.Shared.Kernel.Cqrs;

namespace ModularMonolith.Catalog.Features.GetProductPrice;

/// <summary><c>GET /api/v1/catalog/products/{id}/price?currency=EUR</c>.</summary>
internal sealed class GetProductPriceEndpoint(IQueryHandler<GetProductPriceQuery, ProductPrice> handler)
    : Endpoint<GetProductPriceRequest, ProductPrice>
{
    public override void Configure()
    {
        Get("catalog/products/{id:guid}/price");
        Version(1);
        Options(builder => builder.WithTags("Catalog").RequireRateLimiting(RateLimitPolicies.PerUser));
        Summary(summary => summary.Summary = "Gets a product price, optionally converted to another currency.");
    }

    public override async Task HandleAsync(GetProductPriceRequest req, CancellationToken ct)
    {
        var result = await handler.HandleAsync(new GetProductPriceQuery(req.Id, req.Currency), ct);
        if (result.IsFailure)
        {
            await Send.ResultAsync(result.Error.ToProblem());
            return;
        }

        await Send.OkAsync(result.Value, ct);
    }
}

/// <summary>Route and query-string binding (slice-local DTO).</summary>
internal sealed class GetProductPriceRequest
{
    /// <summary>Gets the product id (route).</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the optional target currency (query string).</summary>
    [QueryParam]
    public string? Currency { get; init; }
}
