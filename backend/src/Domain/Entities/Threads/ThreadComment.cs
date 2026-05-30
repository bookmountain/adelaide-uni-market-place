using Domain.Entities.Users;

namespace Domain.Entities.Threads;

public class ThreadComment
{
    private ThreadComment() { }

    public ThreadComment(Guid id, Guid postId, Guid? parentCommentId, Guid authorUserId, bool isAnonymous, string body)
    {
        Id = id;
        PostId = postId;
        ParentCommentId = parentCommentId;
        AuthorUserId = authorUserId;
        IsAnonymous = isAnonymous;
        Body = body;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid PostId { get; private set; }
    public Guid? ParentCommentId { get; private set; }
    public Guid AuthorUserId { get; private set; }
    public bool IsAnonymous { get; private set; }
    public string Body { get; private set; } = string.Empty;
    public int LikeCount { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }

    public User? Author { get; private set; }

    public void UpdateBody(string body)
    {
        Body = body;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void AdjustLikeCount(int delta)
    {
        LikeCount += delta;
        if (LikeCount < 0) LikeCount = 0;
    }

    public void SoftDelete() => IsDeleted = true;
}
