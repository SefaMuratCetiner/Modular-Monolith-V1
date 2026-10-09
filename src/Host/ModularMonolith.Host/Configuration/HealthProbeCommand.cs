using System.Globalization;

namespace ModularMonolith.Host.Configuration;

/// <summary>
/// <c>dotnet ModularMonolith.Host.dll healthcheck [url]</c>: probes the running instance's readiness
/// endpoint and exits with 0 (HTTP 2xx) or 1. The chiseled runtime image has no shell, curl or wget,
/// so container healthchecks (Docker <c>HEALTHCHECK</c>, compose) call the application itself.
/// Kubernetes uses HTTP probes instead and does not need it.
/// It runs before anything else in <c>Program.cs</c>: no configuration, DI or logging is built.
/// </summary>
internal static class HealthProbeCommand
{
    public const string Name = "healthcheck";

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(3);

    public static async Task<int> RunAsync(string[] args)
    {
        var url = args is [_, var explicitUrl, ..] ? explicitUrl : DefaultUrl();
        using var timeout = new CancellationTokenSource(Timeout);
        using var client = new HttpClient();
        try
        {
            using var response = await client.GetAsync(new Uri(url), HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            return response.IsSuccessStatusCode ? 0 : 1;
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException or UriFormatException)
        {
            return 1;
        }
    }

    // ASPNETCORE_HTTP_PORTS is set by the .NET container images (8080); the first port is probed.
    private static string DefaultUrl()
    {
        var ports = Environment.GetEnvironmentVariable("ASPNETCORE_HTTP_PORTS");
        var port = string.IsNullOrWhiteSpace(ports)
            ? "8080"
            : ports.Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)[0];
        return string.Create(CultureInfo.InvariantCulture, $"http://127.0.0.1:{port}/health/ready");
    }
}
