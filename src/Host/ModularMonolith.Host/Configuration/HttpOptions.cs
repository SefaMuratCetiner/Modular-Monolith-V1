namespace ModularMonolith.Host.Configuration;

/// <summary>
/// HTTP hosting settings (section <c>Http</c>).
/// </summary>
internal sealed class HttpOptions
{
    public const string SectionName = "Http";

    /// <summary>Gets or sets the default request timeout in seconds (504 when exceeded).</summary>
    public int RequestTimeoutSeconds { get; set; } = 30;

    /// <summary>Gets or sets the maximum request body size in bytes (413 when exceeded).</summary>
    public long MaxRequestBodyBytes { get; set; } = 1_048_576;

    /// <summary>Gets or sets a value indicating whether HTTPS redirection and HSTS apply outside Development.</summary>
    public bool EnforceHttps { get; set; } = true;

    /// <summary>Gets or sets the public HTTPS port used for redirects (behind a proxy this is usually 443).</summary>
    public int HttpsPort { get; set; } = 443;

    /// <summary>Gets or sets the HSTS max-age in days.</summary>
    public int HstsMaxAgeDays { get; set; } = 365;
}
