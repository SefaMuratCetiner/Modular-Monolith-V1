using FluentValidation;
using ModularMonolith.Shared.Kernel.Cqrs;
using ModularMonolith.Shared.Kernel.Results;

namespace ModularMonolith.Shared.Infrastructure.Cqrs;

/// <summary>
/// Runs every FluentValidation validator for the command and short-circuits with a
/// <see cref="ValidationError"/> (HTTP 400) before a transaction is opened.
/// </summary>
internal sealed class ValidationCommandDecorator<TCommand, TResponse>(
    ICommandHandler<TCommand, TResponse> inner,
    IEnumerable<IValidator<TCommand>> validators) : ICommandHandler<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    public async Task<Result<TResponse>> HandleAsync(TCommand command, CancellationToken cancellationToken)
    {
        var context = new ValidationContext<TCommand>(command);
        var failures = new List<FluentValidation.Results.ValidationFailure>();
        foreach (var validator in validators)
        {
            var validation = await validator.ValidateAsync(context, cancellationToken);
            failures.AddRange(validation.Errors);
        }

        if (failures.Count == 0)
        {
            return await inner.HandleAsync(command, cancellationToken);
        }

        var errors = failures
            .GroupBy(failure => failure.PropertyName, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Select(failure => failure.ErrorMessage).ToArray(), StringComparer.Ordinal);
        return Result.Failure<TResponse>(new ValidationError(errors));
    }
}
