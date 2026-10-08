using FastEndpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using ModularMonolith.Catalog.Domain;
using ModularMonolith.Shared.Infrastructure.Web;
using ChangeProductPriceHandler = ModularMonolith.Shared.Kernel.Cqrs.ICommandHandler<
    ModularMonolith.Catalog.Features.ChangeProductPrice.ChangeProductPriceCommand, int>;

namespace ModularMonolith.Catalog.Features.ChangeProductPrice;

/// <summary>
/// <c>PUT /api/v1/catalog/products/{id}/price</c>: replaces the price. The body carries the
/// <c>version</c> the client read; a stale version returns 409 (reload and retry).
/// </summary>
internal sealed class ChangeProductPriceEndpoint(ChangeProductPriceHandler handler)
    : Endpoint<ChangeProductPriceRequest, ChangeProductPriceResponse>
{
    public override void Configure()
    {
        Put("catalog/products/{id:guid}/price");
        Version(1);
        Options(builder => builder.WithTags("Catalog").RequireRateLimiting(RateLimitPolicies.PerUser));
        Description(
            builder => builder
                .Accepts<ChangeProductPriceRequest>("application/json")
                .Produces<ChangeProductPriceResponse>(StatusCodes.Status200OK)
                .ProducesProblems(
                    StatusCodes.Status400BadRequest,
                    StatusCodes.Status403Forbidden,
                    StatusCodes.Status404NotFound,
                    StatusCodes.Status409Conflict,
                    StatusCodes.Status503ServiceUnavailable),
            clearDefaults: true);
        Summary(summary => summary.Summary = "Changes a product's price (optimistic concurrency via version).");
    }

    public override async Task HandleAsync(ChangeProductPriceRequest req, CancellationToken ct)
    {
        var result = await handler.HandleAsync(new ChangeProductPriceCommand(new ProductId(req.Id), req.Price, req.Currency, req.Version), ct);
        if (result.IsFailure)
        {
            await Send.ResultAsync(result.Error.ToProblem());
            return;
        }

        await Send.OkAsync(new ChangeProductPriceResponse(req.Id, req.Price, req.Currency.Trim().ToUpperInvariant(), result.Value), ct);
    }
}

/// <summary>Route (<c>id</c>) and body (<c>price</c>, <c>currency</c>, <c>version</c>).</summary>
internal sealed class ChangeProductPriceRequest
{
    /// <summary>Gets the product id (route).</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the new price.</summary>
    public decimal Price { get; init; }

    /// <summary>Gets the ISO 4217 currency.</summary>
    public string Currency { get; init; } = string.Empty;

    /// <summary>Gets the version the client read.</summary>
    public int Version { get; init; }
}

/// <summary>The product's new price and version.</summary>
internal sealed record ChangeProductPriceResponse(Guid Id, decimal Price, string Currency, int Version);
