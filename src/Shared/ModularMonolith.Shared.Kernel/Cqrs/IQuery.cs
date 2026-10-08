namespace ModularMonolith.Shared.Kernel.Cqrs;

/// <summary>
/// A read-only request. Queries never open a write transaction.
/// </summary>
/// <typeparam name="TResponse">The value returned on success.</typeparam>
#pragma warning disable CA1040 // Marker interface: it carries the response type for compile-time handler matching.
public interface IQuery<TResponse>;
#pragma warning restore CA1040
