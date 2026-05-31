using Application.Common.Interfaces;
using Domain.Entities.Notifications;
using Domain.Shared.Enums;
using Microsoft.EntityFrameworkCore;

namespace Application.Notifications;

public sealed class NotificationService
{
    private readonly IApplicationDbContext _db;
    public NotificationService(IApplicationDbContext db) => _db = db;

    public async Task OnCommentCreatedAsync(Guid postId, Guid commentId, CancellationToken ct)
    {
        // DB-existence idempotency: at most one reply-notification per source comment.
        if (await _db.Notifications.AnyAsync(n => n.SourceCommentId == commentId, ct))
        {
            return;
        }

        var comment = await _db.ThreadComments.AsNoTracking().FirstOrDefaultAsync(c => c.Id == commentId, ct);
        if (comment is null)
        {
            return;
        }

        Guid recipientId;
        NotificationType type;

        if (comment.ParentCommentId is { } parentId)
        {
            var parent = await _db.ThreadComments.AsNoTracking().FirstOrDefaultAsync(c => c.Id == parentId, ct);
            if (parent is null) return;
            recipientId = parent.AuthorUserId;
            type = NotificationType.CommentReplied;
        }
        else
        {
            var post = await _db.ThreadPosts.AsNoTracking().FirstOrDefaultAsync(p => p.Id == postId, ct);
            if (post is null) return;
            recipientId = post.AuthorUserId;
            type = NotificationType.PostReplied;
        }

        if (recipientId == comment.AuthorUserId)
        {
            return; // no self-notifications
        }

        // Preserve anonymity: snapshot the actor's anon handle instead of their identity.
        Guid? actorUserId = null;
        string? actorHandle = null;
        if (comment.IsAnonymous)
        {
            var actor = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == comment.AuthorUserId, ct);
            actorHandle = actor?.AnonHandle ?? "anonymous";
        }
        else
        {
            actorUserId = comment.AuthorUserId;
        }

        _db.Notifications.Add(Notification.ForReply(recipientId, type, postId, comment.Id, actorUserId, actorHandle));
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // A concurrent delivery already created this notification (unique index on SourceCommentId). Benign.
        }
    }
}
