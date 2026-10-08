using FastEndpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using ModularMonolith.Catalog.Features.GetProduct;
using ModularMonolith.Shared.Infrastructure.Idempotency;
using ModularMonolith.Shared.Infrastructure.Web;
using CreateProductHandler = ModularMonolith.Shared.Kernel.Cqrs.ICommandHandler<
    ModularMonolith.Catalog.Features.CreateProduct.CreateProductCommand, System.Guid>;

namespace ModularMonolith.Catalog.Features.CreateProduct;

/// <summary>
/// <c>POST /api/v1/catalog/products</c>. Thin adapter: HTTP to command, Result to HTTP.
/// Supports the <c>Idempotency-Key</c> header.
/// </summary>
internal sealed class CreateProductEndpoint(CreateProductHandler handler)
    : Endpoint<CreateProductRequest, CreateProductResponse>
{
    public override void Configure()
    {
        Post("catalog/products");
        Version(1);
        Options(builder => builder.WithTags("Catalog").RequireRateLimiting(RateLimitPolicies.PerUser));
        PreProcessor<IdempotencyPreProcessor<CreateProductRequest>>();
        PostProcessor<IdempotencyPostProcessor<CreateProductRequest, CreateProductResponse>>();
        Description(
            builder => builder
                .Accepts<CreateProductRequest>("application/json")
                .Produces<CreateProductResponse>(StatusCodes.Status201Created)
                .ProducesProblems(
                    StatusCodes.Status400BadRequest,
                    StatusCodes.Status403Forbidden,
                    StatusCodes.Status409Conflict,
                    StatusCodes.Status422UnprocessableEntity,
                    StatusCodes.Status503ServiceUnavailable),
            clearDefaults: true);
        Summary(summary =>
        {
            summary.Summary = "Creates a product in the caller's tenant.";
            summary.Description = "Send an Idempotency-Key header to make retries safe.";
        });
    }

    public override async Task HandleAsync(CreateProductRequest req, CancellationToken ct)
    {
        var result = await handler.HandleAsync(new CreateProductCommand(req.Name, req.Sku, req.Price, req.Currency), ct);
        if (result.IsFailure)
        {
            await Send.ResultAsync(result.Error.ToProblem());
            return;
        }

        HttpContext.Response.Headers.Location = GetProductEndpoint.LocationFor(result.Value);
        await Send.ResponseAsync(new CreateProductResponse(result.Value), StatusCodes.Status201Created, ct);
    }
}

/// <summary>HTTP request body (slice-local DTO; never shared with other slices or modules).</summary>
internal sealed record CreateProductRequest(string Name, string Sku, decimal Price, string Currency);

/// <summary>HTTP response body.</summary>
internal sealed record CreateProductResponse(Guid Id);
