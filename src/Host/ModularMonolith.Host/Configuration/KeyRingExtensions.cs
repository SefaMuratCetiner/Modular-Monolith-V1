using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using ModularMonolith.Shared.Infrastructure.Configuration;
using ModularMonolith.Shared.Infrastructure.Security;
using Npgsql;

namespace ModularMonolith.Host.Configuration;

/// <summary>
/// Persists the Data Protection key ring in PostgreSQL so session cookies and antiforgery tokens stay
/// valid across restarts, redeployments and replicas, and encrypts the keys at rest with a certificate.
/// </summary>
internal static class KeyRingExtensions
{
    public static WebApplicationBuilder AddKeyRing(this WebApplicationBuilder builder)
    {
        builder.Services.AddValidatedOptions<KeyRingOptions, KeyRingOptionsValidator>(builder.Configuration, KeyRingOptions.SectionName);

        // The certificate must be known while the Data Protection builder is configured.
        var settings = builder.Configuration.GetSection(KeyRingOptions.SectionName).Get<KeyRingOptions>() ?? new KeyRingOptions();
        var dataProtection = builder.Services
            .AddDataProtection()
            .SetApplicationName(settings.ApplicationName);

        builder.Services.AddOptions<KeyManagementOptions>()
            .Configure<NpgsqlDataSource>((keyManagement, dataSource) => keyManagement.XmlRepository = new PostgresXmlRepository(dataSource));

        if (!string.IsNullOrWhiteSpace(settings.CertificatePath) && File.Exists(settings.CertificatePath))
        {
            var certificate = X509CertificateLoader.LoadPkcs12FromFile(settings.CertificatePath, settings.CertificatePassword);
            dataProtection
                .ProtectKeysWithCertificate(certificate)
                .UnprotectKeysWithAnyCertificate(certificate);
        }

        return builder;
    }
}
