using FastEndpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using ModularMonolith.Shared.Infrastructure.Web;
using ModularMonolith.Shared.Kernel.Context;
using ExportPersonalDataHandler = ModularMonolith.Shared.Kernel.Cqrs.ICommandHandler<
    ModularMonolith.Privacy.Features.ExportPersonalData.ExportPersonalDataCommand,
    ModularMonolith.Privacy.Features.ExportPersonalData.PersonalDataExport>;

namespace ModularMonolith.Privacy.Features.ExportPersonalData;

/// <summary>
/// <c>GET /api/v1/privacy/me/personal-data</c>: everything the application stores about the caller, as one
/// machine-readable JSON document (downloaded as <c>personal-data.json</c>).
/// </summary>
internal sealed class ExportPersonalDataEndpoint(ExportPersonalDataHandler handler, ICurrentUser currentUser)
    : EndpointWithoutRequest<PersonalDataExport>
{
    public override void Configure()
    {
        Get("privacy/me/personal-data");
        Version(1);
        Policies(PrivacyPermissions.PersonalDataExport.Name);
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

        var result = await handler.HandleAsync(new ExportPersonalDataCommand(subjectId), ct);
        if (result.IsFailure)
        {
            await Send.ResultAsync(result.Error.ToProblem());
            return;
        }

        HttpContext.Response.Headers.CacheControl = "no-store";
        HttpContext.Response.Headers.ContentDisposition = "attachment; filename=\"personal-data.json\"";
        await Send.OkAsync(result.Value, ct);
    }
}
