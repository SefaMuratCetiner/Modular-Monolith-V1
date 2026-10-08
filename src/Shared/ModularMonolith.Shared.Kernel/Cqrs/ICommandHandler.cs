using ModularMonolith.Shared.Kernel.Results;

namespace ModularMonolith.Shared.Kernel.Cqrs;

/// <summary>
/// Handles a command. Resolved directly from DI (no mediator) and wrapped by decorators:
/// logging, telemetry, validation and transaction.
/// </summary>
/// <typeparam name="TCommand">The command type.</typeparam>
/// <typeparam name="TResponse">The success value type.</typeparam>
public interface ICommandHandler<in TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    /// <summary>Handles the command.</summary>
    /// <param name="command">The command.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result<TResponse>> HandleAsync(TCommand command, CancellationToken cancellationToken);
}
