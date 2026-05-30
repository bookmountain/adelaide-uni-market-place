using Contracts.DTO.Threads;

namespace Application.Threads.Indexing;

public sealed record ThreadPostDocument(
    Guid PostId,
    string CategorySlug,
    AuthorRef Author,
    string Title,
    string Body,
    string? ThumbnailKey,
    int LikeCount,
    int CommentCount,
    double HotRank,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastActivityAt);
