using Microsoft.AspNetCore.Http;
using ModularMonolith.Shared.Kernel.Context;

namespace ModularMonolith.Shared.Infrastructure.Security;

/// <summary>
/// <see cref="ICurrentUser"/> backed by the current HTTP principal. Outside HTTP requests
/// (background jobs, message consumers) the user is anonymous and audits as <c>system</c>.
/// </summary>
internal sealed class HttpCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private const string SystemUser = "system";

    public bool IsAuthenticated => httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated == true;

    public string? UserId => httpContextAccessor.HttpContext?.User.FindFirst("sub")?.Value;

    public string AuditId => UserId ?? SystemUser;

    public bool IsInRole(string role) => httpContextAccessor.HttpContext?.User.IsInRole(role) == true;
}
