namespace ModularMonolith.Shared.Infrastructure.Caching;

/// <summary>
/// Cache settings (section <c>Cache</c>).
/// </summary>
public sealed class CacheOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "Cache";

    /// <summary>Gets or sets the Valkey connection string name (<c>ConnectionStrings:{name}</c>).</summary>
    public string ConnectionStringName { get; set; } = "cache";
}
