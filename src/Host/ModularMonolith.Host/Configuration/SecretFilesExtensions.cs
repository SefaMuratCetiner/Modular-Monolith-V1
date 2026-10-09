namespace ModularMonolith.Host.Configuration;

/// <summary>
/// Secret-store integration without a vendor SDK: every file in <c>Secrets:Directory</c> (default
/// <c>/run/secrets</c>) becomes one configuration key, named after the file with <c>__</c> as the section
/// separator. <c>/run/secrets/ConnectionStrings__postgres</c> therefore sets <c>ConnectionStrings:postgres</c>.
/// <para>
/// This is the format all common secret stores can deliver:
/// Docker/Compose secrets, Kubernetes Secret volumes, the Secrets Store CSI driver, External Secrets
/// Operator, and HashiCorp Vault / OpenBao Agent templates. The application never holds credentials for the
/// secret store itself, and no secret is committed or passed on the command line.
/// </para>
/// Secret files override every other source (they are added last). Values are read at startup; restart
/// (roll) the deployment after rotating a secret. A missing directory is ignored, so local development
/// uses <c>dotnet user-secrets</c> instead (see <c>deploy/init-secrets.ps1</c>).
/// </summary>
internal static class SecretFilesExtensions
{
    public const string DirectoryKey = "Secrets:Directory";

    public const string DefaultDirectory = "/run/secrets";

    public static ConfigurationManager AddSecretFiles(this ConfigurationManager configuration)
    {
        var directory = Path.GetFullPath(configuration[DirectoryKey] ?? DefaultDirectory);
        configuration.AddKeyPerFile(directory, optional: true, reloadOnChange: false);
        return configuration;
    }
}
