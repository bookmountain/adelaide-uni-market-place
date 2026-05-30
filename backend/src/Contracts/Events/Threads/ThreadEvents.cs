namespace Contracts.Events.Threads;

public sealed record ThreadPostCreated(Guid PostId);
public sealed record ThreadPostUpdated(Guid PostId);
public sealed record ThreadPostDeleted(Guid PostId);
public sealed record ThreadPostLikeChanged(Guid PostId);
public sealed record ThreadCommentCreated(Guid PostId, Guid CommentId);
public sealed record ThreadCommentDeleted(Guid PostId, Guid CommentId);
public sealed record ThreadCommentLikeChanged(Guid PostId, Guid CommentId);
