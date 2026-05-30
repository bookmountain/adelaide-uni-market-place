namespace Contracts.DTO.Threads;

public sealed record ThreadPostSummary(
    Guid Id,
    string CategorySlug,
    AuthorRef Author,
    string Title,
    string Excerpt,
    string? ThumbnailKey,
    int LikeCount,
    int CommentCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastActivityAt);
