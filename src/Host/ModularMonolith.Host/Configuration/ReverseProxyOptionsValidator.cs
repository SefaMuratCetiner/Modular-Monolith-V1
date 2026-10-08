using System.Net;
using Microsoft.Extensions.Options;
using ModularMonolith.Shared.Infrastructure.Configuration;

namespace ModularMonolith.Host.Configuration;

/// <summary>Fails startup when a trusted proxy address or network cannot be parsed.</summary>
internal sealed class ReverseProxyOptionsValidator : IValidateOptions<ReverseProxyOptions>
{
    public ValidateOptionsResult Validate(string? name, ReverseProxyOptions options)
    {
        var failures = new List<string>();
        failures.AddRange(options.KnownProxies
            .Where(proxy => !IPAddress.TryParse(proxy, out _))
            .Select(proxy => $"KnownProxies entry '{proxy}' is not an IP address."));
        failures.AddRange(options.KnownNetworks
            .Where(network => !IPNetwork.TryParse(network, out _))
            .Select(network => $"KnownNetworks entry '{network}' is not a CIDR network."));
        if (options.ForwardLimit is < 1 or > 10)
        {
            failures.Add("ForwardLimit must be between 1 and 10.");
        }

        return OptionsValidation.ToResult(ReverseProxyOptions.SectionName, failures);
    }
}
