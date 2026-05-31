using Application.Notifications;
using Application.UnitTests.Common;
using Domain.Entities.Threads;
using Domain.Entities.Users;
using Domain.Shared.Enums;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Application.UnitTests.Notifications;

public sealed class NotificationServiceTests
{
    private static User NewUser(Guid id, string? anon = null)
    {
        var u = new User(id, $"{id:N}@adelaide.edu.au", "Name", DateTimeOffset.UtcNow, "Student", "hash",
            AdelaideDepartment.ComputerScience, AcademicDegree.Bachelor, UserSex.Other, isActive: true);
        if (anon is not null) u.AssignAnonHandle(anon);
        return u;
    }

    private static async Task<(MarketplaceDbContext db, Guid postAuthor, ThreadPost post)> SeedPost(TestDb t)
    {
        var postAuthor = Guid.NewGuid(); var catId = Guid.NewGuid();
        t.Context.Users.Add(NewUser(postAuthor));
        t.Context.ThreadCategories.Add(new ThreadCategory(catId, "general", "General", "x", "chat", 1));
        var post = new ThreadPost(Guid.NewGuid(), catId, postAuthor, false, "T", "B");
        t.Context.ThreadPosts.Add(post);
        await t.Context.SaveChangesAsync();
        return (t.Context, postAuthor, post);
    }

    [Fact]
    public async Task Top_level_comment_notifies_post_author()
    {
        await using var t = await TestDb.CreateAsync();
        var (db, postAuthor, post) = await SeedPost(t);
        var commenter = Guid.NewGuid();
        db.Users.Add(NewUser(commenter));
        var comment = new ThreadComment(Guid.NewGuid(), post.Id, null, commenter, false, "hi");
        db.ThreadComments.Add(comment);
        await db.SaveChangesAsync();

        await new NotificationService(db).OnCommentCreatedAsync(post.Id, comment.Id, default);

        var notif = await db.Notifications.SingleAsync();
        Assert.Equal(postAuthor, notif.RecipientUserId);
        Assert.Equal(NotificationType.PostReplied, notif.Type);
        Assert.Equal(commenter, notif.ActorUserId);
    }

    [Fact]
    public async Task Anonymous_commenter_stores_handle_snapshot_not_user()
    {
        await using var t = await TestDb.CreateAsync();
        var (db, postAuthor, post) = await SeedPost(t);
        var commenter = Guid.NewGuid();
        db.Users.Add(NewUser(commenter, anon: "quiet-koala-4821"));
        var comment = new ThreadComment(Guid.NewGuid(), post.Id, null, commenter, isAnonymous: true, "hi");
        db.ThreadComments.Add(comment);
        await db.SaveChangesAsync();

        await new NotificationService(db).OnCommentCreatedAsync(post.Id, comment.Id, default);

        var notif = await db.Notifications.SingleAsync();
        Assert.Null(notif.ActorUserId);
        Assert.Equal("quiet-koala-4821", notif.ActorAnonHandleSnapshot);
    }

    [Fact]
    public async Task Reply_notifies_parent_comment_author()
    {
        await using var t = await TestDb.CreateAsync();
        var (db, _, post) = await SeedPost(t);
        var parentAuthor = Guid.NewGuid(); var replier = Guid.NewGuid();
        db.Users.Add(NewUser(parentAuthor)); db.Users.Add(NewUser(replier));
        var parent = new ThreadComment(Guid.NewGuid(), post.Id, null, parentAuthor, false, "parent");
        db.ThreadComments.Add(parent);
        var reply = new ThreadComment(Guid.NewGuid(), post.Id, parent.Id, replier, false, "reply");
        db.ThreadComments.Add(reply);
        await db.SaveChangesAsync();

        await new NotificationService(db).OnCommentCreatedAsync(post.Id, reply.Id, default);

        var notif = await db.Notifications.SingleAsync(n => n.SourceCommentId == reply.Id);
        Assert.Equal(parentAuthor, notif.RecipientUserId);
        Assert.Equal(NotificationType.CommentReplied, notif.Type);
    }

    [Fact]
    public async Task Self_reply_creates_no_notification()
    {
        await using var t = await TestDb.CreateAsync();
        var (db, postAuthor, post) = await SeedPost(t);
        var comment = new ThreadComment(Guid.NewGuid(), post.Id, null, postAuthor, false, "my own");
        db.ThreadComments.Add(comment);
        await db.SaveChangesAsync();

        await new NotificationService(db).OnCommentCreatedAsync(post.Id, comment.Id, default);
        Assert.Equal(0, await db.Notifications.CountAsync());
    }

    [Fact]
    public async Task Duplicate_delivery_is_idempotent()
    {
        await using var t = await TestDb.CreateAsync();
        var (db, _, post) = await SeedPost(t);
        var commenter = Guid.NewGuid();
        db.Users.Add(NewUser(commenter));
        var comment = new ThreadComment(Guid.NewGuid(), post.Id, null, commenter, false, "hi");
        db.ThreadComments.Add(comment);
        await db.SaveChangesAsync();

        var svc = new NotificationService(db);
        await svc.OnCommentCreatedAsync(post.Id, comment.Id, default);
        await svc.OnCommentCreatedAsync(post.Id, comment.Id, default); // redelivery
        Assert.Equal(1, await db.Notifications.CountAsync());
    }
}
