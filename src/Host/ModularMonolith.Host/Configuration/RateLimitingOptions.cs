namespace ModularMonolith.Host.Configuration;

/// <summary>
/// Rate limiting settings (section <c>RateLimiting</c>).
/// </summary>
internal sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    /// <summary>Gets or sets the requests allowed per window, per tenant and user.</summary>
    public int PermitLimit { get; set; } = 100;

    /// <summary>Gets or sets the fixed window length in seconds.</summary>
    public int WindowSeconds { get; set; } = 60;
}
