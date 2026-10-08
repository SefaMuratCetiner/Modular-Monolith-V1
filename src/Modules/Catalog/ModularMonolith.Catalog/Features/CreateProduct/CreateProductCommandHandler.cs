using Microsoft.FeatureManagement;
using ModularMonolith.Catalog.Domain;
using ModularMonolith.Catalog.Infrastructure;
using ModularMonolith.Shared.Kernel.Context;
using ModularMonolith.Shared.Kernel.Cqrs;
using ModularMonolith.Shared.Kernel.Results;

namespace ModularMonolith.Catalog.Features.CreateProduct;

/// <summary>
/// Business logic of the slice. It returns <see cref="Result{T}"/> errors for expected failures
/// and knows nothing about HTTP, transactions, auditing or the outbox (those are decorators).
/// </summary>
internal sealed class CreateProductCommandHandler(
    IProductRepository products,
    ICurrentUser currentUser,
    IFeatureManager featureManager,
    TimeProvider timeProvider) : ICommandHandler<CreateProductCommand, ProductId>
{
    public async Task<Result<ProductId>> HandleAsync(CreateProductCommand command, CancellationToken cancellationToken)
    {
        if (await featureManager.IsEnabledAsync(CatalogFeatures.WriteDisabled, cancellationToken))
        {
            return Result.Failure<ProductId>(CatalogErrors.WritesDisabled);
        }

        var seller = SellerId.Create(currentUser.UserId);
        if (seller.IsFailure)
        {
            return Result.Failure<ProductId>(seller.Error);
        }

        var created = Product.Create(command.Name, command.Sku, command.Price, command.Currency, seller.Value, timeProvider.GetUtcNow());
        if (created.IsFailure)
        {
            return Result.Failure<ProductId>(created.Error);
        }

        if (await products.SkuExistsAsync(created.Value.Sku, cancellationToken))
        {
            return Result.Failure<ProductId>(CatalogErrors.SkuTaken);
        }

        await products.AddAsync(created.Value, cancellationToken);
        return Result.Success(created.Value.Id);
    }
}
