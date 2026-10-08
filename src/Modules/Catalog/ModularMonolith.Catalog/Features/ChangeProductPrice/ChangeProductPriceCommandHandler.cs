using Microsoft.FeatureManagement;
using ModularMonolith.Catalog.Domain;
using ModularMonolith.Catalog.Infrastructure;
using ModularMonolith.Shared.Kernel.Cqrs;
using ModularMonolith.Shared.Kernel.Results;

namespace ModularMonolith.Catalog.Features.ChangeProductPrice;

/// <summary>
/// Loads the aggregate, lets it enforce its invariants and saves it with an optimistic-concurrency
/// check. A stale <c>ExpectedVersion</c> (or a concurrent writer) yields 409 instead of a lost update.
/// </summary>
internal sealed class ChangeProductPriceCommandHandler(
    IProductRepository products,
    IFeatureManager featureManager,
    TimeProvider timeProvider) : ICommandHandler<ChangeProductPriceCommand, int>
{
    public async Task<Result<int>> HandleAsync(ChangeProductPriceCommand command, CancellationToken cancellationToken)
    {
        if (await featureManager.IsEnabledAsync(CatalogFeatures.WriteDisabled, cancellationToken))
        {
            return Result.Failure<int>(CatalogErrors.WritesDisabled);
        }

        var product = await products.GetAsync(command.ProductId, cancellationToken);
        if (product is null)
        {
            return Result.Failure<int>(CatalogErrors.ProductNotFound);
        }

        if (product.Version != command.ExpectedVersion)
        {
            return Result.Failure<int>(Error.ConcurrencyConflict);
        }

        var changed = product.ChangePrice(command.Price, command.Currency, timeProvider.GetUtcNow());
        if (changed.IsFailure)
        {
            return Result.Failure<int>(changed.Error);
        }

        if (product.DomainEvents.Count == 0)
        {
            return Result.Success(product.Version); // Same price: nothing to save.
        }

        return await products.UpdatePriceAsync(product, cancellationToken)
            ? Result.Success(product.Version + 1)
            : Result.Failure<int>(Error.ConcurrencyConflict);
    }
}
