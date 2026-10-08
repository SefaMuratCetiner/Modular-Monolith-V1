using System.Reflection;
using ModularMonolith.Shared.Infrastructure.Data;

namespace ModularMonolith.Shared.Infrastructure.Modules;

/// <summary>
/// Registry of the modules composed into the host. Drives handler scanning, migrations,
/// outbox jobs, endpoint discovery and integration-event type resolution.
/// </summary>
public sealed class ModuleCatalog
{
    private readonly IModule[] _modules;

    /// <summary>Initializes a new instance of the <see cref="ModuleCatalog"/> class.</summary>
    /// <param name="modules">The composed modules.</param>
    public ModuleCatalog(IEnumerable<IModule> modules)
    {
        ArgumentNullException.ThrowIfNull(modules);
        _modules = [.. modules];

        foreach (var module in _modules)
        {
            // Throws for anything that is not a safe, unquoted PostgreSQL identifier.
            _ = SqlIdentifier.Quote(module.Schema);
        }

        var duplicate = _modules
            .GroupBy(module => module.Schema, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidOperationException($"Schema '{duplicate.Key}' is claimed by more than one module.");
        }
    }

    /// <summary>Gets the composed modules.</summary>
    public IReadOnlyList<IModule> Modules => _modules;

    /// <summary>Finds the module owning an implementation or contracts assembly.</summary>
    /// <param name="assembly">The assembly.</param>
    /// <returns>The owning module.</returns>
    public IModule ForAssembly(Assembly assembly)
        => _modules.FirstOrDefault(module => module.Assembly == assembly || module.ContractsAssembly == assembly)
           ?? throw new InvalidOperationException($"Assembly '{assembly?.GetName().Name}' does not belong to a registered module.");

    /// <summary>Finds a module by schema name.</summary>
    /// <param name="schema">The schema.</param>
    /// <returns>The module, or <see langword="null"/>.</returns>
    public IModule? FindBySchema(string schema)
        => _modules.FirstOrDefault(module => string.Equals(module.Schema, schema, StringComparison.Ordinal));
}
