using FastEndpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using ModularMonolith.Catalog.Domain;
using ModularMonolith.Shared.Infrastructure.Web;
using ModularMonolith.Shared.Kernel.Results;
using ChangeProductPriceHandler = ModularMonolith.Shared.Kernel.Cqrs.ICommandHandler<
    ModularMonolith.Catalog.Features.ChangeProductPrice.ChangeProductPriceCommand, int>;

namespace ModularMonolith.Catalog.Features.ChangeProductPrice;

/// <summary>
/// <c>PUT /api/v1/catalog/products/{id}/price</c>: replaces the price. The client states which version
/// it read, either as <c>If-Match: "&lt;etag&gt;"</c> (stale: 412) or as <c>version</c> in the body (stale: 409).
/// Without either the request is rejected with 428 Precondition Required.
/// </summary>
internal sealed class ChangeProductPriceEndpoint(ChangeProductPriceHandler handler)
    : Endpoint<ChangeProductPriceRequest, ChangeProductPriceResponse>
{
    public override void Configure()
    {
        Put("catalog/products/{id:guid}/price");
        Version(1);
        Policies(CatalogPermissions.ProductsWrite.Name);
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
                    StatusCodes.Status412PreconditionFailed,
                    StatusCodes.Status428PreconditionRequired,
                    StatusCodes.Status503ServiceUnavailable),
            clearDefaults: true);
        Summary(summary => summary.Summary = "Changes a product's price (optimistic concurrency via If-Match or version).");
    }

    public override async Task HandleAsync(ChangeProductPriceRequest req, CancellationToken ct)
    {
        // The expected version comes from If-Match (preferred, HTTP-native) or from the body.
        var ifMatch = EntityTags.ReadIfMatch(HttpContext.Request);
        if (ifMatch.IsMalformed || (ifMatch.IsPresent && req.Version is { } bodyVersion && bodyVersion != ifMatch.Version))
        {
            await ProblemResponses.WriteAsync(HttpContext, StatusCodes.Status400BadRequest, "The precondition is invalid.", "If-Match must be one ETag returned by GET and must agree with the body version.");
            return;
        }

        var expectedVersion = ifMatch.IsPresent ? ifMatch.Version : req.Version;
        if (expectedVersion is not { } version)
        {
            await ProblemResponses.WriteAsync(HttpContext, StatusCodes.Status428PreconditionRequired, "A precondition is required.", "Send the product's ETag in If-Match (or its version in the body) to avoid lost updates.");
            return;
        }

        var result = await handler.HandleAsync(new ChangeProductPriceCommand(new ProductId(req.Id), req.Price, req.Currency, version), ct);
        if (result.IsFailure)
        {
            if (ifMatch.IsPresent && result.Error == Error.ConcurrencyConflict)
            {
                await ProblemResponses.WriteAsync(HttpContext, StatusCodes.Status412PreconditionFailed, "The precondition failed.", result.Error.Message);
                return;
            }

            await Send.ResultAsync(result.Error.ToProblem());
            return;
        }

        await Send.OkAsync(new ChangeProductPriceResponse(req.Id, req.Price, req.Currency.Trim().ToUpperInvariant(), result.Value), ct);
    }
}

/// <summary>Route (<c>id</c>) and body (<c>price</c>, <c>currency</c>, optional <c>version</c>).</summary>
internal sealed class ChangeProductPriceRequest
{
    /// <summary>Gets the product id (route).</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the new price.</summary>
    public decimal Price { get; init; }

    /// <summary>Gets the ISO 4217 currency.</summary>
    public string Currency { get; init; } = string.Empty;

    /// <summary>Gets the version the client read (alternative to the If-Match header).</summary>
    public int? Version { get; init; }
}

/// <summary>The product's new price and version.</summary>
internal sealed record ChangeProductPriceResponse(Guid Id, decimal Price, string Currency, int Version);
