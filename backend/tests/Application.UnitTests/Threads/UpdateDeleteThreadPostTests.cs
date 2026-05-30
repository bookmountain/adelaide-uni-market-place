using Application.Threads.Commands.DeleteThreadPost;
using Application.Threads.Commands.UpdateThreadPost;
using Application.UnitTests.Common;
using Domain.Entities.Threads;
using Domain.Entities.Users;
using Domain.Shared.Enums;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Application.UnitTests.Threads;

public sealed class UpdateDeleteThreadPostTests
{
    private static async Task<(MarketplaceDbContext db, Guid ownerId, ThreadPost post)> Seed(TestDb t)
    {
        var ownerId = Guid.NewGuid(); var catId = Guid.NewGuid();
        t.Context.Users.Add(new User(ownerId, "s@adelaide.edu.au", "Sarah", DateTimeOffset.UtcNow, "Student", "hash",
            AdelaideDepartment.ComputerScience, AcademicDegree.Bachelor, UserSex.Other, isActive: true));
        t.Context.ThreadCategories.Add(new ThreadCategory(catId, "general", "General", "x", "chat", 1));
        var post = new ThreadPost(Guid.NewGuid(), catId, ownerId, false, "T", "B");
        t.Context.ThreadPosts.Add(post);
        await t.Context.SaveChangesAsync();
        return (t.Context, ownerId, post);
    }

    [Fact]
    public async Task Owner_can_update_body()
    {
        await using var t = await TestDb.CreateAsync();
        var (db, ownerId, post) = await Seed(t);
        await new UpdateThreadPostCommandHandler(db, new Infrastructure.Outbox.Outbox(db)).Handle(
            new UpdateThreadPostCommand(post.Id, ownerId, "New T", "New B"), default);
        var saved = await db.ThreadPosts.FirstAsync(p => p.Id == post.Id);
        Assert.Equal("New T", saved.Title);
    }

    [Fact]
    public async Task Non_owner_cannot_update()
    {
        await using var t = await TestDb.CreateAsync();
        var (db, _, post) = await Seed(t);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            new UpdateThreadPostCommandHandler(db, new Infrastructure.Outbox.Outbox(db)).Handle(
                new UpdateThreadPostCommand(post.Id, Guid.NewGuid(), "X", "Y"), default));
    }

    [Fact]
    public async Task Admin_can_soft_delete_others_post()
    {
        await using var t = await TestDb.CreateAsync();
        var (db, _, post) = await Seed(t);
        await new DeleteThreadPostCommandHandler(db, new Infrastructure.Outbox.Outbox(db)).Handle(
            new DeleteThreadPostCommand(post.Id, Guid.NewGuid(), IsAdmin: true), default);
        var saved = await db.ThreadPosts.FirstAsync(p => p.Id == post.Id);
        Assert.True(saved.IsDeleted);
    }

    [Fact]
    public async Task Non_owner_non_admin_cannot_delete()
    {
        await using var t = await TestDb.CreateAsync();
        var (db, _, post) = await Seed(t);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            new DeleteThreadPostCommandHandler(db, new Infrastructure.Outbox.Outbox(db)).Handle(
                new DeleteThreadPostCommand(post.Id, Guid.NewGuid(), IsAdmin: false), default));
    }
}
