using Microsoft.Extensions.Options;
using ModularMonolith.Shared.Infrastructure.Configuration;

namespace ModularMonolith.Host.Configuration;

/// <summary>Fails startup when the key ring would be stored unencrypted where encryption is required.</summary>
internal sealed class KeyRingOptionsValidator : IValidateOptions<KeyRingOptions>
{
    public ValidateOptionsResult Validate(string? name, KeyRingOptions options)
    {
        var failures = new List<string>();
        if (string.IsNullOrWhiteSpace(options.ApplicationName))
        {
            failures.Add("ApplicationName is required (all replicas must share it).");
        }

        if (string.IsNullOrWhiteSpace(options.CertificatePath))
        {
            if (options.RequireEncryptionAtRest)
            {
                failures.Add("CertificatePath is required to encrypt the key ring at rest (set RequireEncryptionAtRest=false only for local development).");
            }
        }
        else if (!File.Exists(options.CertificatePath))
        {
            failures.Add($"CertificatePath '{options.CertificatePath}' does not exist.");
        }

        return OptionsValidation.ToResult(KeyRingOptions.SectionName, failures);
    }
}
