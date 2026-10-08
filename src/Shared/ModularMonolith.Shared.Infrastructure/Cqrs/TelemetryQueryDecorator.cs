using ModularMonolith.Shared.Kernel.Cqrs;
using ModularMonolith.Shared.Kernel.Results;

namespace ModularMonolith.Shared.Infrastructure.Cqrs;

/// <summary>Query decorator that emits a trace span and a duration metric.</summary>
internal sealed class TelemetryQueryDecorator<TQuery, TResponse>(
    IQueryHandler<TQuery, TResponse> inner) : IQueryHandler<TQuery, TResponse>
    where TQuery : IQuery<TResponse>
{
    public Task<Result<TResponse>> HandleAsync(TQuery query, CancellationToken cancellationToken)
        => HandlerTelemetry.TraceAsync("query", typeof(TQuery).Name, () => inner.HandleAsync(query, cancellationToken));
}
