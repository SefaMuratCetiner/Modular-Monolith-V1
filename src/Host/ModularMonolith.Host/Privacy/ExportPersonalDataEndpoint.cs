using FastEndpoints;
using ModularMonolith.Host.Configuration;
using ModularMonolith.Shared.Infrastructure.Web;
using ModularMonolith.Shared.Kernel.Context;
using ModularMonolith.Shared.Kernel.Privacy;

namespace ModularMonolith.Host.Privacy;

/// <summary>
/// <c>GET /api/v1/privacy/me/personal-data</c>: right of access and data portability (GDPR Art. 15 and 20).
/// Combines what every module stores about the caller (<see cref="IPersonalDataExporter"/>) into one
/// machine-readable JSON document. Modules export only their own tables, in the caller's tenant.
/// </summary>
internal sealed class ExportPersonalDataEndpoint(
    IEnumerable<IPersonalDataExporter> exporters,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    ILogger<ExportPersonalDataEndpoint> logger) : EndpointWithoutRequest<PersonalDataExport>
{
    public override void Configure()
    {
        Get("privacy/me/personal-data");
        Version(1);
        Policies(PlatformPermissions.PersonalDataExport.Name);
        Options(builder => builder.WithTags("Privacy").RequireRateLimiting(RateLimitPolicies.PerUser));
        Description(
            builder => builder
                .Produces<PersonalDataExport>(StatusCodes.Status200OK)
                .ProducesProblems(StatusCodes.Status403Forbidden),
            clearDefaults: true);
        Summary(summary => summary.Summary = "Exports all personal data the application stores about the authenticated user.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (currentUser.UserId is not { } subjectId)
        {
            await ProblemResponses.WriteAsync(HttpContext, StatusCodes.Status403Forbidden, "The operation is not allowed.", "An authenticated user is required.");
            return;
        }

        // Sequential on purpose: the exporters share the request's database session.
        var sections = new SortedDictionary<string, object?>(StringComparer.Ordinal);
        foreach (var exporter in exporters)
        {
            sections[exporter.Section] = await exporter.ExportAsync(subjectId, ct);
        }

        HostLog.PersonalDataExported(logger, subjectId, sections.Count);
        HttpContext.Response.Headers.CacheControl = "no-store";
        HttpContext.Response.Headers.ContentDisposition = "attachment; filename=\"personal-data.json\"";
        await Send.OkAsync(new PersonalDataExport(subjectId, timeProvider.GetUtcNow(), sections), ct);
    }
}

/// <summary>The export document: one section per module.</summary>
/// <param name="SubjectId">The identity provider subject of the user.</param>
/// <param name="ExportedAt">When the export was produced.</param>
/// <param name="Sections">Module sections (<see langword="null"/> when a module holds nothing).</param>
internal sealed record PersonalDataExport(string SubjectId, DateTimeOffset ExportedAt, IReadOnlyDictionary<string, object?> Sections);
