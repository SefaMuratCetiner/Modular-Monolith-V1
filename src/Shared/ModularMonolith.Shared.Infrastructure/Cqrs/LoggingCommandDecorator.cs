using Microsoft.Extensions.Logging;
using ModularMonolith.Shared.Infrastructure.Observability;
using ModularMonolith.Shared.Kernel.Cqrs;
using ModularMonolith.Shared.Kernel.Results;

namespace ModularMonolith.Shared.Infrastructure.Cqrs;

/// <summary>Outermost command decorator: structured logging of the outcome.</summary>
internal sealed class LoggingCommandDecorator<TCommand, TResponse>(
    ICommandHandler<TCommand, TResponse> inner,
    ILogger<LoggingCommandDecorator<TCommand, TResponse>> logger) : ICommandHandler<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    public async Task<Result<TResponse>> HandleAsync(TCommand command, CancellationToken cancellationToken)
    {
        var name = typeof(TCommand).Name;
        logger.Handling("command", name);
        var result = await inner.HandleAsync(command, cancellationToken);
        if (result.IsFailure)
        {
            logger.HandlerFailed("command", name, result.Error.Code);
        }
        else
        {
            logger.Handled("command", name);
        }

        return result;
    }
}
