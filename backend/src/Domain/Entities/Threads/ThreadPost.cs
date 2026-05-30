using Domain.Entities.Users;

namespace Domain.Entities.Threads;

public class ThreadPost
{
    private readonly List<ThreadPostImage> _images = new();

    private ThreadPost() { }

    public ThreadPost(Guid id, Guid categoryId, Guid authorUserId, bool isAnonymous, string title, string body)
    {
        Id = id;
        CategoryId = categoryId;
        AuthorUserId = authorUserId;
        IsAnonymous = isAnonymous;
        Title = title;
        Body = body;
        CreatedAt = DateTimeOffset.UtcNow;
        LastActivityAt = CreatedAt;
    }

    public Guid Id { get; private set; }
    public Guid CategoryId { get; private set; }
    public Guid AuthorUserId { get; private set; }
    public bool IsAnonymous { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;
    public int LikeCount { get; private set; }
    public int CommentCount { get; private set; }
    public DateTimeOffset LastActivityAt { get; private set; }
    public bool IsPinned { get; private set; }
    public bool IsLocked { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }

    public ThreadCategory? Category { get; private set; }
    public User? Author { get; private set; }
    public IReadOnlyCollection<ThreadPostImage> Images => _images;

    public void UpdateContent(string title, string body)
    {
        Title = title;
        Body = body;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void AddImage(ThreadPostImage image) => _images.Add(image);

    public void AdjustLikeCount(int delta)
    {
        LikeCount += delta;
        if (LikeCount < 0) LikeCount = 0;
    }

    public void RegisterCommentAdded(DateTimeOffset at)
    {
        CommentCount += 1;
        LastActivityAt = at;
    }

    public void RegisterCommentRemoved()
    {
        CommentCount -= 1;
        if (CommentCount < 0) CommentCount = 0;
    }

    public void SoftDelete() => IsDeleted = true;
    public void SetLocked(bool locked) => IsLocked = locked;
    public void SetPinned(bool pinned) => IsPinned = pinned;
}
