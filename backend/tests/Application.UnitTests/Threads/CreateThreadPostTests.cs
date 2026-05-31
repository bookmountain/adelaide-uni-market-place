using Application.Threads.Commands.CreateThreadPost;
using Application.UnitTests.Common;
using Application.UnitTests.TestDoubles;
using Domain.Entities.Threads;
using Domain.Entities.Users;
using Domain.Shared.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Application.UnitTests.Threads;

public sealed class CreateThreadPostTests
{
    private static User NewUser(Guid id) => new(id, "s@adelaide.edu.au", "Sarah", DateTimeOffset.UtcNow, "Student",
        "hash", AdelaideDepartment.ComputerScience, AcademicDegree.Bachelor, UserSex.Other, isActive: true);
    private static ThreadCategory NewCategory(Guid id) => new(id, "general", "General", "x", "chat", 1);

    [Fact]
    public async Task Creates_real_post_in_category()
    {
        await using var db = await TestDb.CreateAsync();
        var userId = Guid.NewGuid(); var catId = Guid.NewGuid();
        db.Context.Users.Add(NewUser(userId));
        db.Context.ThreadCategories.Add(NewCategory(catId));
        await db.Context.SaveChangesAsync();

        var handler = new CreateThreadPostCommandHandler(db.Context, new CreateThreadPostTestsFakes.FakeStorage(), new CreateThreadPostTestsFakes.FakeSender(db.Context), new Infrastructure.Outbox.EfOutbox(db.Context));
        var postId = await handler.Handle(
            new CreateThreadPostCommand(userId, catId, "Title", "Body", IsAnonymous: false, Images: new List<ThreadPostImageUpload>()), default);

        var post = await db.Context.ThreadPosts.FirstAsync(p => p.Id == postId);
        Assert.Equal("Title", post.Title);
        Assert.False(post.IsAnonymous);
        Assert.Equal(catId, post.CategoryId);
    }

    [Fact]
    public async Task Anonymous_post_triggers_handle_assignment_and_uploads_images()
    {
        await using var db = await TestDb.CreateAsync();
        var userId = Guid.NewGuid(); var catId = Guid.NewGuid();
        db.Context.Users.Add(NewUser(userId));
        db.Context.ThreadCategories.Add(NewCategory(catId));
        await db.Context.SaveChangesAsync();

        var storage = new CreateThreadPostTestsFakes.FakeStorage();
        var handler = new CreateThreadPostCommandHandler(db.Context, storage, new CreateThreadPostTestsFakes.FakeSender(db.Context), new Infrastructure.Outbox.EfOutbox(db.Context));
        var images = new List<ThreadPostImageUpload> { new(new byte[] { 1, 2, 3 }, "image/png", "a.png") };
        var postId = await handler.Handle(new CreateThreadPostCommand(userId, catId, "T", "B", true, images), default);

        var user = await db.Context.Users.FirstAsync(u => u.Id == userId);
        Assert.False(string.IsNullOrWhiteSpace(user.AnonHandle));
        Assert.Single(storage.Keys);
        var saved = await db.Context.ThreadPosts.Include(p => p.Images).FirstAsync(p => p.Id == postId);
        Assert.Single(saved.Images);
    }

    [Fact]
    public async Task Rejects_unknown_or_inactive_category()
    {
        await using var db = await TestDb.CreateAsync();
        var userId = Guid.NewGuid();
        db.Context.Users.Add(NewUser(userId));
        await db.Context.SaveChangesAsync();

        var handler = new CreateThreadPostCommandHandler(db.Context, new CreateThreadPostTestsFakes.FakeStorage(), new CreateThreadPostTestsFakes.FakeSender(db.Context), new Infrastructure.Outbox.EfOutbox(db.Context));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new CreateThreadPostCommand(userId, Guid.NewGuid(), "T", "B", false, new List<ThreadPostImageUpload>()), default));
    }
}
