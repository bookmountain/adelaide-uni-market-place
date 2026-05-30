namespace Contracts.DTO.Threads;

public sealed record ThreadPostDetailResponse(
    Guid Id,
    string CategorySlug,
    AuthorRef Author,
    string Title,
    string Body,
    IReadOnlyList<string> ImageKeys,
    int LikeCount,
    int CommentCount,
    bool IsLocked,
    bool IsPinned,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastActivityAt);
