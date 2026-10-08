using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.Localization;

namespace ModularMonolith.Shared.Infrastructure.Localization;

/// <summary>
/// Creates one <see cref="JsonStringLocalizer"/> per assembly, so <c>IStringLocalizer&lt;T&gt;</c>
/// resolves resources embedded next to <c>T</c> (i.e. in the owning module).
/// </summary>
internal sealed class JsonStringLocalizerFactory : IStringLocalizerFactory
{
    private readonly ConcurrentDictionary<Assembly, JsonStringLocalizer> _localizers = new();

    public IStringLocalizer Create(Type resourceSource)
    {
        ArgumentNullException.ThrowIfNull(resourceSource);
        return _localizers.GetOrAdd(resourceSource.Assembly, assembly => new JsonStringLocalizer(assembly));
    }

    public IStringLocalizer Create(string baseName, string location)
        => _localizers.GetOrAdd(Assembly.Load(new AssemblyName(location)), assembly => new JsonStringLocalizer(assembly));
}
