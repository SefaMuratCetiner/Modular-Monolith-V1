using Microsoft.Extensions.Logging;
using ModularMonolith.Shared.Infrastructure.Observability;
using ModularMonolith.Shared.Kernel.Cqrs;
using ModularMonolith.Shared.Kernel.Results;

namespace ModularMonolith.Shared.Infrastructure.Cqrs;

/// <summary>Outermost query decorator: structured logging of the outcome.</summary>
internal sealed class LoggingQueryDecorator<TQuery, TResponse>(
    IQueryHandler<TQuery, TResponse> inner,
    ILogger<LoggingQueryDecorator<TQuery, TResponse>> logger) : IQueryHandler<TQuery, TResponse>
    where TQuery : IQuery<TResponse>
{
    public async Task<Result<TResponse>> HandleAsync(TQuery query, CancellationToken cancellationToken)
    {
        var name = typeof(TQuery).Name;
        logger.Handling("query", name);
        var result = await inner.HandleAsync(query, cancellationToken);
        if (result.IsFailure)
        {
            logger.HandlerFailed("query", name, result.Error.Code);
        }
        else
        {
            logger.Handled("query", name);
        }

        return result;
    }
}
