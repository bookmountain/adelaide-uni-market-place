namespace Contracts.DTO.Threads;

/// <summary>
/// Author projection that enforces anonymity at the API boundary.
/// Anonymous content carries ONLY <see cref="Handle"/>; real content carries identity.
/// </summary>
public sealed record AuthorRef(
    bool IsAnonymous,
    string? Handle,
    Guid? UserId,
    string? DisplayName,
    string? AvatarUrl);
