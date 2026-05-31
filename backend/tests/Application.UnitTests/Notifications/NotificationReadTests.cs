using Application.Notifications.Commands.MarkAllNotificationsRead;
using Application.Notifications.Commands.MarkNotificationRead;
using Application.Notifications.Queries.GetNotifications;
using Application.Notifications.Queries.GetUnreadCount;
using Application.UnitTests.Common;
using Domain.Entities.Notifications;
using Domain.Entities.Users;
using Domain.Shared.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Application.UnitTests.Notifications;

public sealed class NotificationReadTests
{
    private static User NewUser(Guid id) => new(id, $"{id:N}@adelaide.edu.au", "Name", DateTimeOffset.UtcNow, "Student",
        "hash", AdelaideDepartment.ComputerScience, AcademicDegree.Bachelor, UserSex.Other, isActive: true);

    [Fact]
    public async Task List_returns_recipient_notifications_with_anon_actor_hidden()
    {
        await using var t = await TestDb.CreateAsync();
        var recipient = Guid.NewGuid(); var postId = Guid.NewGuid();
        t.Context.Notifications.Add(Notification.ForReply(recipient, NotificationType.PostReplied, postId, Guid.NewGuid(),
            actorUserId: null, actorAnonHandle: "quiet-koala-4821"));
        await t.Context.SaveChangesAsync();

        var list = await new GetNotificationsQueryHandler(t.Context).Handle(new GetNotificationsQuery(recipient, null, 20), default);

        var item = Assert.Single(list.Items);
        Assert.True(item.Actor.IsAnonymous);
        Assert.Equal("quiet-koala-4821", item.Actor.Handle);
        Assert.Null(item.Actor.UserId);
    }

    [Fact]
    public async Task Unread_count_counts_only_unread_for_recipient()
    {
        await using var t = await TestDb.CreateAsync();
        var recipient = Guid.NewGuid();
        var read = Notification.ForReply(recipient, NotificationType.PostReplied, Guid.NewGuid(), null, Guid.NewGuid(), null);
        read.MarkRead();
        t.Context.Notifications.Add(read);
        t.Context.Notifications.Add(Notification.ForReply(recipient, NotificationType.PostReplied, Guid.NewGuid(), null, Guid.NewGuid(), null));
        t.Context.Notifications.Add(Notification.ForReply(Guid.NewGuid(), NotificationType.PostReplied, Guid.NewGuid(), null, Guid.NewGuid(), null));
        await t.Context.SaveChangesAsync();

        var count = await new GetUnreadCountQueryHandler(t.Context).Handle(new GetUnreadCountQuery(recipient), default);
        Assert.Equal(1, count.UnreadCount);
    }

    [Fact]
    public async Task Mark_read_only_affects_own_notification()
    {
        await using var t = await TestDb.CreateAsync();
        var recipient = Guid.NewGuid();
        var n = Notification.ForReply(recipient, NotificationType.PostReplied, Guid.NewGuid(), null, Guid.NewGuid(), null);
        t.Context.Notifications.Add(n);
        await t.Context.SaveChangesAsync();

        await new MarkNotificationReadCommandHandler(t.Context).Handle(new MarkNotificationReadCommand(recipient, n.Id), default);
        Assert.True((await t.Context.Notifications.SingleAsync()).IsRead);

        // A different user cannot mark it (no-op, no throw)
        await new MarkNotificationReadCommandHandler(t.Context).Handle(new MarkNotificationReadCommand(Guid.NewGuid(), n.Id), default);
    }

    [Fact]
    public async Task Mark_all_marks_every_unread_for_recipient()
    {
        await using var t = await TestDb.CreateAsync();
        var recipient = Guid.NewGuid();
        t.Context.Notifications.Add(Notification.ForReply(recipient, NotificationType.PostReplied, Guid.NewGuid(), null, Guid.NewGuid(), null));
        t.Context.Notifications.Add(Notification.ForReply(recipient, NotificationType.CommentReplied, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null));
        await t.Context.SaveChangesAsync();

        await new MarkAllNotificationsReadCommandHandler(t.Context).Handle(new MarkAllNotificationsReadCommand(recipient), default);
        Assert.Equal(0, await t.Context.Notifications.CountAsync(n => !n.IsRead));
    }
}
