using ModularMonolith.Identity.Domain;

namespace ModularMonolith.Identity.Infrastructure;

/// <summary>Write-side persistence for <see cref="UserProfile"/>.</summary>
internal interface IUserProfileRepository
{
    Task<bool> SubjectExistsAsync(SubjectId subjectId, CancellationToken cancellationToken);

    Task<bool> EmailExistsAsync(Email email, CancellationToken cancellationToken);

    Task AddAsync(UserProfile profile, CancellationToken cancellationToken);
}
