using ModularMonolith.Identity.Domain;

namespace ModularMonolith.Identity.Infrastructure;

/// <summary>Write-side persistence for <see cref="UserProfile"/>.</summary>
internal interface IUserProfileRepository
{
    Task<bool> SubjectExistsAsync(SubjectId subjectId, CancellationToken cancellationToken);

    Task<bool> EmailExistsAsync(Email email, CancellationToken cancellationToken);

    Task<UserProfile?> GetBySubjectAsync(SubjectId subjectId, CancellationToken cancellationToken);

    Task AddAsync(UserProfile profile, CancellationToken cancellationToken);

    /// <summary>Anonymizes and soft-deletes the profile; <see langword="false"/> on a concurrency conflict.</summary>
    Task<bool> EraseAsync(UserProfile profile, CancellationToken cancellationToken);
}
