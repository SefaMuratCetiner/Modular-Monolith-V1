using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace ModularMonolith.Host.Configuration;

/// <summary>
/// Graceful shutdown on SIGTERM:
/// <list type="number">
/// <item>Readiness (<c>/health/ready</c>) turns unhealthy immediately, so load balancers stop routing here.</item>
/// <item>The host waits <c>Http:ShutdownDrainSeconds</c> while still serving, giving them time to notice.</item>
/// <item>Kestrel then stops accepting connections and in-flight requests and background work get up to
/// <c>Http:ShutdownTimeoutSeconds</c> to finish.</item>
/// </list>
/// The orchestrator's grace period (compose <c>stop_grace_period</c>, Kubernetes
/// <c>terminationGracePeriodSeconds</c>) must exceed drain + timeout.
/// </summary>
internal static class GracefulShutdown
{
    public static IServiceCollection AddGracefulShutdown(this IServiceCollection services)
    {
        services.AddSingleton<ShutdownState>();
        services.AddHostedService<ShutdownDrainService>();
        services.AddHealthChecks().AddCheck<ShutdownHealthCheck>("shutdown", tags: ["ready"]);
        services.AddOptions<HostOptions>()
            .Configure<IOptions<HttpOptions>>((host, http) =>
                host.ShutdownTimeout = TimeSpan.FromSeconds(http.Value.ShutdownDrainSeconds + http.Value.ShutdownTimeoutSeconds));
        return services;
    }
}

/// <summary>Process-wide "we are shutting down" flag.</summary>
internal sealed class ShutdownState
{
    private volatile bool _draining;

    public bool IsDraining => _draining;

    public void BeginDraining() => _draining = true;
}

/// <summary>Readiness check that fails as soon as shutdown starts.</summary>
internal sealed class ShutdownHealthCheck(ShutdownState state) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        => Task.FromResult(state.IsDraining
            ? HealthCheckResult.Unhealthy("Shutting down: draining connections.")
            : HealthCheckResult.Healthy());
}

/// <summary>
/// Runs in the "stopping" phase, before any hosted service (including the web server) stops,
/// so requests keep being served during the drain window.
/// </summary>
internal sealed class ShutdownDrainService(
    ShutdownState state,
    IOptions<HttpOptions> options,
    ILogger<ShutdownDrainService> logger) : IHostedLifecycleService
{
    public Task StartingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StartedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task StoppingAsync(CancellationToken cancellationToken)
    {
        state.BeginDraining();
        var drainSeconds = options.Value.ShutdownDrainSeconds;
        HostLog.DrainStarted(logger, drainSeconds);
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(drainSeconds), cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // The shutdown timeout elapsed; continue stopping.
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
