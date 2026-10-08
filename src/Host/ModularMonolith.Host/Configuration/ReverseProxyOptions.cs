namespace ModularMonolith.Host.Configuration;

/// <summary>
/// Trusted reverse proxies whose <c>X-Forwarded-*</c> headers are honoured (section <c>ReverseProxy</c>).
/// Without entries only loopback proxies are trusted.
/// </summary>
internal sealed class ReverseProxyOptions
{
    public const string SectionName = "ReverseProxy";

    /// <summary>Gets or sets trusted proxy IP addresses, e.g. <c>10.0.0.5</c>.</summary>
    public IList<string> KnownProxies { get; set; } = [];

    /// <summary>Gets or sets trusted proxy networks in CIDR notation, e.g. <c>10.0.0.0/8</c>.</summary>
    public IList<string> KnownNetworks { get; set; } = [];

    /// <summary>Gets or sets how many proxy hops are processed.</summary>
    public int ForwardLimit { get; set; } = 1;
}
