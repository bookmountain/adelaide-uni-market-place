using Domain.Shared.Enums;

namespace Domain.Entities.Notifications;

public class Notification
{
    private Notification() { }

    private Notification(Guid id, Guid recipientUserId, NotificationType type, Guid sourcePostId, Guid? sourceCommentId,
        Guid? actorUserId, string? actorAnonHandleSnapshot)
    {
        Id = id;
        RecipientUserId = recipientUserId;
        Type = type;
        SourcePostId = sourcePostId;
        SourceCommentId = sourceCommentId;
        ActorUserId = actorUserId;
        ActorAnonHandleSnapshot = actorAnonHandleSnapshot;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid RecipientUserId { get; private set; }
    public NotificationType Type { get; private set; }
    public Guid SourcePostId { get; private set; }
    public Guid? SourceCommentId { get; private set; }
    public Guid? ActorUserId { get; private set; }
    public string? ActorAnonHandleSnapshot { get; private set; }
    public bool IsRead { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static Notification ForReply(Guid recipientUserId, NotificationType type, Guid sourcePostId, Guid? sourceCommentId,
        Guid? actorUserId, string? actorAnonHandle)
        => new(Guid.NewGuid(), recipientUserId, type, sourcePostId, sourceCommentId, actorUserId, actorAnonHandle);

    public void MarkRead() => IsRead = true;
}
