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
    TimeProvider timeProvider) : ICommandHandler<CreateProductCommand, Guid>
{
    public async Task<Result<Guid>> HandleAsync(CreateProductCommand command, CancellationToken cancellationToken)
    {
        if (await featureManager.IsEnabledAsync(CatalogFeatures.WriteDisabled, cancellationToken))
        {
            return Result.Failure<Guid>(CatalogErrors.WritesDisabled);
        }

        if (await products.SkuExistsAsync(command.Sku, cancellationToken))
        {
            return Result.Failure<Guid>(CatalogErrors.SkuTaken);
        }

        var created = Product.Create(command.Name, command.Sku, command.Price, command.Currency, currentUser.AuditId, timeProvider.GetUtcNow());
        if (created.IsFailure)
        {
            return Result.Failure<Guid>(created.Error);
        }

        await products.AddAsync(created.Value, cancellationToken);
        return Result.Success(created.Value.Id);
    }
}
