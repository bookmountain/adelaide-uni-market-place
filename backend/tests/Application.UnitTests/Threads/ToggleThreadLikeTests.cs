using Application.Threads.Commands.ToggleThreadLike;
using Application.UnitTests.Common;
using Domain.Entities.Threads;
using Domain.Entities.Users;
using Domain.Shared.Enums;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Application.UnitTests.Threads;

public sealed class ToggleThreadLikeTests
{
    private static async Task<(MarketplaceDbContext db, Guid userId, ThreadPost post)> Seed(TestDb t)
    {
        var userId = Guid.NewGuid(); var catId = Guid.NewGuid();
        t.Context.Users.Add(new User(userId, "s@adelaide.edu.au", "Sarah", DateTimeOffset.UtcNow, "Student", "hash",
            AdelaideDepartment.ComputerScience, AcademicDegree.Bachelor, UserSex.Other, isActive: true));
        t.Context.ThreadCategories.Add(new ThreadCategory(catId, "general", "General", "x", "chat", 1));
        var post = new ThreadPost(Guid.NewGuid(), catId, userId, false, "T", "B");
        t.Context.ThreadPosts.Add(post);
        await t.Context.SaveChangesAsync();
        return (t.Context, userId, post);
    }

    [Fact]
    public async Task First_like_adds_and_increments_second_removes()
    {
        await using var t = await TestDb.CreateAsync();
        var (db, userId, post) = await Seed(t);
        var handler = new ToggleThreadLikeCommandHandler(db);

        var r1 = await handler.Handle(new ToggleThreadLikeCommand(userId, ThreadLikeTarget.Post, post.Id), default);
        Assert.True(r1.Liked);
        Assert.Equal(1, r1.LikeCount);

        var r2 = await handler.Handle(new ToggleThreadLikeCommand(userId, ThreadLikeTarget.Post, post.Id), default);
        Assert.False(r2.Liked);
        Assert.Equal(0, r2.LikeCount);

        Assert.False(await db.ThreadLikes.AnyAsync(l => l.UserId == userId && l.TargetId == post.Id));
    }

    [Fact]
    public async Task Like_on_missing_target_throws()
    {
        await using var t = await TestDb.CreateAsync();
        var (db, userId, _) = await Seed(t);
        var handler = new ToggleThreadLikeCommandHandler(db);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new ToggleThreadLikeCommand(userId, ThreadLikeTarget.Post, Guid.NewGuid()), default));
    }
}
