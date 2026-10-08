using ModularMonolith.Shared.Kernel.Cqrs;
using ModularMonolith.Shared.Kernel.Results;

namespace ModularMonolith.Shared.Infrastructure.Cqrs;

/// <summary>Command decorator that emits a trace span and a duration metric.</summary>
internal sealed class TelemetryCommandDecorator<TCommand, TResponse>(
    ICommandHandler<TCommand, TResponse> inner) : ICommandHandler<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    public Task<Result<TResponse>> HandleAsync(TCommand command, CancellationToken cancellationToken)
        => HandlerTelemetry.TraceAsync("command", typeof(TCommand).Name, () => inner.HandleAsync(command, cancellationToken));
}
