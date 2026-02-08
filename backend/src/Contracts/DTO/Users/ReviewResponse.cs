namespace Contracts.DTO.Users;

public sealed record ReviewResponse(
    Guid Id,
    Guid ReviewerId,
    string ReviewerName,
    Guid TargetUserId,
    string TargetUserName,
    int Rating,
    string Comment,
    DateTimeOffset CreatedAt);
