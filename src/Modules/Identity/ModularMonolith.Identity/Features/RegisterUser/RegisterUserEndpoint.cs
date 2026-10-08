using FastEndpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using ModularMonolith.Shared.Infrastructure.Idempotency;
using ModularMonolith.Shared.Infrastructure.Web;
using RegisterUserHandler = ModularMonolith.Shared.Kernel.Cqrs.ICommandHandler<
    ModularMonolith.Identity.Features.RegisterUser.RegisterUserCommand, System.Guid>;

namespace ModularMonolith.Identity.Features.RegisterUser;

/// <summary><c>POST /api/v1/identity/users/me</c>: registers the caller's profile.</summary>
internal sealed class RegisterUserEndpoint(RegisterUserHandler handler)
    : Endpoint<RegisterUserRequest, RegisterUserResponse>
{
    public override void Configure()
    {
        Post("identity/users/me");
        Version(1);
        Options(builder => builder.WithTags("Identity").RequireRateLimiting(RateLimitPolicies.PerUser));
        PreProcessor<IdempotencyPreProcessor<RegisterUserRequest>>();
        PostProcessor<IdempotencyPostProcessor<RegisterUserRequest, RegisterUserResponse>>();
        Description(
            builder => builder
                .Accepts<RegisterUserRequest>("application/json")
                .Produces<RegisterUserResponse>(StatusCodes.Status201Created)
                .ProducesProblems(
                    StatusCodes.Status400BadRequest,
                    StatusCodes.Status403Forbidden,
                    StatusCodes.Status409Conflict,
                    StatusCodes.Status422UnprocessableEntity),
            clearDefaults: true);
        Summary(summary => summary.Summary = "Registers a profile for the authenticated user.");
    }

    public override async Task HandleAsync(RegisterUserRequest req, CancellationToken ct)
    {
        var result = await handler.HandleAsync(new RegisterUserCommand(req.Email, req.DisplayName), ct);
        if (result.IsFailure)
        {
            await Send.ResultAsync(result.Error.ToProblem());
            return;
        }

        HttpContext.Response.Headers.Location = "/api/v1/identity/users/me";
        await Send.ResponseAsync(new RegisterUserResponse(result.Value), StatusCodes.Status201Created, ct);
    }
}

/// <summary>HTTP request body.</summary>
internal sealed record RegisterUserRequest(string Email, string DisplayName);

/// <summary>HTTP response body.</summary>
internal sealed record RegisterUserResponse(Guid Id);
