using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace ModularMonolith.Shared.Infrastructure.Caching;

/// <summary>
/// Owns the Valkey connection. It is opened asynchronously during startup
/// (<c>InitializeModulesAsync</c>), so no request thread ever blocks on a synchronous connect.
/// With <c>AbortOnConnectFail=false</c> startup continues while Valkey is down and the client
/// reconnects in the background (the cache and idempotency store degrade meanwhile).
/// </summary>
public sealed class ValkeyConnection(IConfiguration configuration, IOptions<CacheOptions> options) : IAsyncDisposable
{
    private readonly SemaphoreSlim _connectLock = new(1, 1);
    private ConnectionMultiplexer? _multiplexer;

    /// <summary>Gets the connected multiplexer.</summary>
    /// <exception cref="InvalidOperationException">Startup did not call <see cref="ConnectAsync"/>.</exception>
    public IConnectionMultiplexer Multiplexer
        => _multiplexer ?? throw new InvalidOperationException("Valkey is not connected; ConnectAsync must run during startup.");

    /// <summary>Opens the connection (idempotent).</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        if (_multiplexer is not null)
        {
            return;
        }

        await _connectLock.WaitAsync(cancellationToken);
        try
        {
            if (_multiplexer is not null)
            {
                return;
            }

            var name = options.Value.ConnectionStringName;
            var connectionString = configuration.GetConnectionString(name)
                ?? throw new InvalidOperationException($"Connection string '{name}' is not configured.");
            var settings = ConfigurationOptions.Parse(connectionString);
            settings.AbortOnConnectFail = false;
            _multiplexer = await ConnectionMultiplexer.ConnectAsync(settings);
        }
        finally
        {
            _connectLock.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_multiplexer is not null)
        {
            await _multiplexer.DisposeAsync();
        }

        _connectLock.Dispose();
    }
}
