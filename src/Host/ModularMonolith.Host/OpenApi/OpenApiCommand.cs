using NSwag.Generation;

namespace ModularMonolith.Host.OpenApi;

/// <summary>
/// <c>dotnet ModularMonolith.Host.dll openapi [--check] [path]</c>: writes the OpenAPI contract of
/// API version 1 to a file (default <see cref="DefaultPath"/>) and exits, without starting the server or
/// touching any database. With <c>--check</c> it compares instead of writing and exits with 1 when the
/// committed contract is out of date, so CI catches undocumented API changes.
/// The file is committed and versioned with the code; review its diff like any other public API change.
/// </summary>
internal static class OpenApiCommand
{
    public const string Name = "openapi";

    /// <summary>The OpenAPI document name of API version 1.</summary>
    public const string DocumentName = "v1";

    public const string DefaultPath = "docs/openapi/modular-monolith.v1.json";

    private const string CheckOption = "--check";

    public static bool IsRequested(string[] args) => args is [Name, ..];

    public static async Task<int> RunAsync(WebApplication app, string[] args)
    {
        var check = args.Contains(CheckOption, StringComparer.Ordinal);
        var path = args.Skip(1).FirstOrDefault(argument => !argument.StartsWith("--", StringComparison.Ordinal)) ?? DefaultPath;
        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(OpenApiCommand));

        var document = await app.Services.GetRequiredService<IOpenApiDocumentGenerator>().GenerateAsync(DocumentName);
        var json = document.ToJson().ReplaceLineEndings("\n") + "\n";

        if (check)
        {
            var committed = File.Exists(path) ? (await File.ReadAllTextAsync(path)).ReplaceLineEndings("\n") : string.Empty;
            if (!string.Equals(committed, json, StringComparison.Ordinal))
            {
                HostLog.OpenApiOutdated(logger, path, Name);
                return 1;
            }

            HostLog.OpenApiUpToDate(logger, path);
            return 0;
        }

        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(path, json);
        HostLog.OpenApiWritten(logger, path);
        return 0;
    }
}
