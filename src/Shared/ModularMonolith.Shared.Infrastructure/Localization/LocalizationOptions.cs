namespace ModularMonolith.Shared.Infrastructure.Localization;

/// <summary>
/// Request localization settings (section <c>Localization</c>).
/// </summary>
public sealed class LocalizationOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "Localization";

    /// <summary>Gets or sets the supported cultures; the first one is the default.</summary>
    public IList<string> SupportedCultures { get; set; } = ["en"];
}
