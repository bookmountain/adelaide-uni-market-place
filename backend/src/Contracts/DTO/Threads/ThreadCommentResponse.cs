namespace Contracts.DTO.Threads;

public sealed record ThreadCommentResponse(
    Guid Id,
    Guid? ParentCommentId,
    AuthorRef Author,
    string Body,
    int LikeCount,
    bool IsDeleted,
    DateTimeOffset CreatedAt,
    IReadOnlyList<ThreadCommentResponse> Replies);
