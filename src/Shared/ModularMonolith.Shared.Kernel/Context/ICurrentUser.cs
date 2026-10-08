namespace ModularMonolith.Shared.Kernel.Context;

/// <summary>
/// The acting principal. Modules depend on this abstraction instead of HttpContext.
/// </summary>
public interface ICurrentUser
{
    /// <summary>Gets a value indicating whether the caller is authenticated.</summary>
    bool IsAuthenticated { get; }

    /// <summary>Gets the subject identifier (<c>sub</c> claim), or <see langword="null"/>.</summary>
    string? UserId { get; }

    /// <summary>Gets the identifier used for audit columns (<c>sub</c> or <c>system</c>).</summary>
    string AuditId { get; }

    /// <summary>Checks whether the caller has a role.</summary>
    /// <param name="role">The role name.</param>
    /// <returns><see langword="true"/> when the caller is in the role.</returns>
    bool IsInRole(string role);
}
