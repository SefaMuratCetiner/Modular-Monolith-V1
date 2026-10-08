using ModularMonolith.Shared.Kernel.Results;

namespace ModularMonolith.Shared.Kernel.Cqrs;

/// <summary>
/// Handles a query. Wrapped by logging and telemetry decorators.
/// </summary>
/// <typeparam name="TQuery">The query type.</typeparam>
/// <typeparam name="TResponse">The success value type.</typeparam>
public interface IQueryHandler<in TQuery, TResponse>
    where TQuery : IQuery<TResponse>
{
    /// <summary>Handles the query.</summary>
    /// <param name="query">The query.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result<TResponse>> HandleAsync(TQuery query, CancellationToken cancellationToken);
}
