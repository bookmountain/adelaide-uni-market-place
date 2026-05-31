using Domain.Entities.Moderation;
using Domain.Entities.Notifications;
using Domain.Shared.Enums;
using Xunit;

namespace Application.UnitTests.Moderation;

public sealed class ModerationAggregatesTests
{
    [Fact]
    public void New_report_is_open()
    {
        var r = new ThreadReport(Guid.NewGuid(), Guid.NewGuid(), ReportTargetType.Post, Guid.NewGuid(), ReportReason.Spam, "scammy");
        Assert.Equal(ReportStatus.Open, r.Status);
        Assert.Null(r.ReviewedByUserId);
    }

    [Fact]
    public void Resolve_sets_status_reviewer_and_time()
    {
        var r = new ThreadReport(Guid.NewGuid(), Guid.NewGuid(), ReportTargetType.Comment, Guid.NewGuid(), ReportReason.Harassment, null);
        var admin = Guid.NewGuid();
        r.Resolve(admin, ReportStatus.Reviewed);
        Assert.Equal(ReportStatus.Reviewed, r.Status);
        Assert.Equal(admin, r.ReviewedByUserId);
        Assert.NotNull(r.ReviewedAt);
    }

    [Fact]
    public void Audit_captures_admin_action()
    {
        var a = ModerationAudit.Record(Guid.NewGuid(), ReportTargetType.Post, Guid.NewGuid(), "remove-content", "spam");
        Assert.Equal("remove-content", a.Action);
        Assert.NotEqual(Guid.Empty, a.Id);
    }

    [Fact]
    public void Notification_can_be_marked_read()
    {
        var n = Notification.ForReply(Guid.NewGuid(), NotificationType.PostReplied, Guid.NewGuid(), null,
            actorUserId: Guid.NewGuid(), actorAnonHandle: null);
        Assert.False(n.IsRead);
        n.MarkRead();
        Assert.True(n.IsRead);
    }

    [Fact]
    public void Anonymous_actor_notification_stores_handle_not_user()
    {
        var n = Notification.ForReply(Guid.NewGuid(), NotificationType.CommentReplied, Guid.NewGuid(), Guid.NewGuid(),
            actorUserId: null, actorAnonHandle: "quiet-koala-4821");
        Assert.Null(n.ActorUserId);
        Assert.Equal("quiet-koala-4821", n.ActorAnonHandleSnapshot);
    }
}
