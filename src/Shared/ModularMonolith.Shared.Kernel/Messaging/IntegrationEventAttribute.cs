namespace ModularMonolith.Shared.Kernel.Messaging;

/// <summary>
/// Declares the stable, versioned contract name of an integration event. The outbox stores
/// <c>{Name}.v{Version}</c>, never the CLR type name, so contract classes can be renamed or moved freely.
/// <para>
/// Versioning rules:
/// <list type="bullet">
/// <item>Additive, optional changes (new nullable property) keep the version.</item>
/// <item>Breaking changes (rename, remove, change meaning or type) bump <see cref="Version"/> and ship an
/// upcaster from the previous version, so messages already stored in outboxes stay readable.</item>
/// <item>Never reuse or change a <see cref="Name"/>.</item>
/// </list>
/// </para>
/// </summary>
/// <param name="name">The contract name, e.g. <c>catalog.product-created</c> (lowercase, dot-separated, module first).</param>
/// <param name="version">The schema version (1 or higher).</param>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class IntegrationEventAttribute(string name, int version) : Attribute
{
    /// <summary>Gets the stable contract name.</summary>
    public string Name { get; } = name;

    /// <summary>Gets the current schema version.</summary>
    public int Version { get; } = version;
}
