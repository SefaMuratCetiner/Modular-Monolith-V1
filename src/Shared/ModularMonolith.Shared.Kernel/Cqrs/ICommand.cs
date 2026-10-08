namespace ModularMonolith.Shared.Kernel.Cqrs;

/// <summary>
/// A state-changing request. Commands run inside a transaction (unit of work) and are audited.
/// </summary>
/// <typeparam name="TResponse">The value returned on success.</typeparam>
#pragma warning disable CA1040 // Marker interface: it carries the response type for compile-time handler matching.
public interface ICommand<TResponse>;
#pragma warning restore CA1040
