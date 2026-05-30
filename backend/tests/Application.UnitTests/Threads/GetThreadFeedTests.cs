using Application.Threads.Queries.GetThreadFeed;
using Application.UnitTests.Common;
using Domain.Entities.Threads;
using Domain.Entities.Users;
using Domain.Shared.Enums;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Application.UnitTests.Threads;

public sealed class GetThreadFeedTests
{
    private static async Task<(MarketplaceDbContext db, Guid catId, Guid userId)> Seed(TestDb t)
    {
        var userId = Guid.NewGuid(); var catId = Guid.NewGuid();
        t.Context.Users.Add(new User(userId, "s@adelaide.edu.au", "Sarah", DateTimeOffset.UtcNow, "Student", "hash",
            AdelaideDepartment.ComputerScience, AcademicDegree.Bachelor, UserSex.Other, isActive: true));
        t.Context.ThreadCategories.Add(new ThreadCategory(catId, "general", "General", "x", "chat", 1));
        await t.Context.SaveChangesAsync();
        return (t.Context, catId, userId);
    }

    private static ThreadPost Post(Guid catId, Guid userId, string title, bool anon = false) =>
        new(Guid.NewGuid(), catId, userId, anon, title, "B");

    [Fact]
    public async Task New_sort_returns_most_recent_first()
    {
        await using var t = await TestDb.CreateAsync();
        var (db, catId, userId) = await Seed(t);
        var older = Post(catId, userId, "older"); db.ThreadPosts.Add(older); await db.SaveChangesAsync();
        await Task.Delay(5);
        var newer = Post(catId, userId, "newer"); db.ThreadPosts.Add(newer); await db.SaveChangesAsync();

        var feed = await new GetThreadFeedQueryHandler(db).Handle(
            new GetThreadFeedQuery(CategorySlug: null, Sort: "new", Cursor: null, PageSize: 10), default);

        Assert.Equal("newer", feed.Items[0].Title);
    }

    [Fact]
    public async Task Top_sort_orders_by_like_count()
    {
        await using var t = await TestDb.CreateAsync();
        var (db, catId, userId) = await Seed(t);
        var a = Post(catId, userId, "a"); a.AdjustLikeCount(1);
        var b = Post(catId, userId, "b"); b.AdjustLikeCount(5);
        db.ThreadPosts.AddRange(a, b); await db.SaveChangesAsync();

        var feed = await new GetThreadFeedQueryHandler(db).Handle(
            new GetThreadFeedQuery(null, "top", null, 10), default);

        Assert.Equal("b", feed.Items[0].Title);
    }

    [Fact]
    public async Task Deleted_posts_excluded_and_anon_authors_hidden()
    {
        await using var t = await TestDb.CreateAsync();
        var (db, catId, userId) = await Seed(t);
        var user = await db.Users.FirstAsync(); user.AssignAnonHandle("quiet-koala-4821"); await db.SaveChangesAsync();
        var visible = Post(catId, userId, "visible", anon: true);
        var gone = Post(catId, userId, "gone"); gone.SoftDelete();
        db.ThreadPosts.AddRange(visible, gone); await db.SaveChangesAsync();

        var feed = await new GetThreadFeedQueryHandler(db).Handle(new GetThreadFeedQuery(null, "new", null, 10), default);

        Assert.Single(feed.Items);
        Assert.Equal("visible", feed.Items[0].Title);
        Assert.True(feed.Items[0].Author.IsAnonymous);
        Assert.Equal("quiet-koala-4821", feed.Items[0].Author.Handle);
        Assert.Null(feed.Items[0].Author.UserId);
    }

    [Fact]
    public async Task Cursor_paginates()
    {
        await using var t = await TestDb.CreateAsync();
        var (db, catId, userId) = await Seed(t);
        for (var i = 0; i < 3; i++) { db.ThreadPosts.Add(Post(catId, userId, $"p{i}")); await db.SaveChangesAsync(); await Task.Delay(2); }

        var page1 = await new GetThreadFeedQueryHandler(db).Handle(new GetThreadFeedQuery(null, "new", null, 2), default);
        Assert.Equal(2, page1.Items.Count);
        Assert.NotNull(page1.NextCursor);

        var page2 = await new GetThreadFeedQueryHandler(db).Handle(new GetThreadFeedQuery(null, "new", page1.NextCursor, 2), default);
        Assert.Single(page2.Items);
        Assert.Null(page2.NextCursor);
    }
}
