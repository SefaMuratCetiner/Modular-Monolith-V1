namespace ModularMonolith.Host.Configuration;

/// <summary>
/// ASP.NET Core Data Protection key ring settings (section <c>DataProtection</c>). The keys protect
/// session cookies and antiforgery tokens; they are stored in PostgreSQL (<c>platform.data_protection_keys</c>)
/// and, outside Development, encrypted at rest with an X.509 certificate.
/// </summary>
internal sealed class KeyRingOptions
{
    public const string SectionName = "DataProtection";

    /// <summary>Gets or sets the application discriminator; all replicas must use the same value.</summary>
    public string ApplicationName { get; set; } = "modular-monolith";

    /// <summary>Gets or sets the path of a PKCS#12 (.pfx) certificate used to encrypt keys at rest.</summary>
    public string? CertificatePath { get; set; }

    /// <summary>Gets or sets the certificate password (from a secret store, never committed).</summary>
    public string? CertificatePassword { get; set; }

    /// <summary>Gets or sets a value indicating whether a certificate is mandatory. Only disable for local development.</summary>
    public bool RequireEncryptionAtRest { get; set; } = true;
}
