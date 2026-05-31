namespace Contracts.Events.Threads;

public static class ThreadEventTypes
{
    public const string PostCreated = "thread.post.created";
    public const string PostUpdated = "thread.post.updated";
    public const string PostDeleted = "thread.post.deleted";
    public const string PostLikeChanged = "thread.post.like-changed";
    public const string CommentCreated = "thread.comment.created";
    public const string CommentDeleted = "thread.comment.deleted";
    public const string CommentLikeChanged = "thread.comment.like-changed";
}
