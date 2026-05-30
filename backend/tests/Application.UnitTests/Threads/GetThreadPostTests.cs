using Application.Threads.Queries.GetThreadPost;
using Application.UnitTests.Common;
using Domain.Entities.Threads;
using Domain.Entities.Users;
using Domain.Shared.Enums;
using Xunit;

namespace Application.UnitTests.Threads;

public sealed class GetThreadPostTests
{
    private static User NewUser(Guid id, string? anon)
    {
        var u = new User(id, "s@adelaide.edu.au", "Sarah", DateTimeOffset.UtcNow, "Student", "hash",
            AdelaideDepartment.ComputerScience, AcademicDegree.Bachelor, UserSex.Other, isActive: true);
        if (anon is not null) u.AssignAnonHandle(anon);
        return u;
    }

    [Fact]
    public async Task Anonymous_post_detail_hides_author_identity()
    {
        await using var db = await TestDb.CreateAsync();
        var userId = Guid.NewGuid(); var catId = Guid.NewGuid();
        db.Context.Users.Add(NewUser(userId, "quiet-koala-4821"));
        db.Context.ThreadCategories.Add(new ThreadCategory(catId, "general", "General", "x", "chat", 1));
        var post = new ThreadPost(Guid.NewGuid(), catId, userId, isAnonymous: true, "T", "B");
        db.Context.ThreadPosts.Add(post);
        await db.Context.SaveChangesAsync();

        var result = await new GetThreadPostQueryHandler(db.Context).Handle(new GetThreadPostQuery(post.Id), default);

        Assert.NotNull(result);
        Assert.True(result!.Author.IsAnonymous);
        Assert.Equal("quiet-koala-4821", result.Author.Handle);
        Assert.Null(result.Author.UserId);
        Assert.Null(result.Author.DisplayName);
    }

    [Fact]
    public async Task Real_post_detail_shows_identity()
    {
        await using var db = await TestDb.CreateAsync();
        var userId = Guid.NewGuid(); var catId = Guid.NewGuid();
        db.Context.Users.Add(NewUser(userId, null));
        db.Context.ThreadCategories.Add(new ThreadCategory(catId, "general", "General", "x", "chat", 1));
        var post = new ThreadPost(Guid.NewGuid(), catId, userId, false, "T", "B");
        db.Context.ThreadPosts.Add(post);
        await db.Context.SaveChangesAsync();

        var result = await new GetThreadPostQueryHandler(db.Context).Handle(new GetThreadPostQuery(post.Id), default);

        Assert.False(result!.Author.IsAnonymous);
        Assert.Equal(userId, result.Author.UserId);
        Assert.Equal("Sarah", result.Author.DisplayName);
    }

    [Fact]
    public async Task Deleted_post_returns_null()
    {
        await using var db = await TestDb.CreateAsync();
        var userId = Guid.NewGuid(); var catId = Guid.NewGuid();
        db.Context.Users.Add(NewUser(userId, null));
        db.Context.ThreadCategories.Add(new ThreadCategory(catId, "general", "General", "x", "chat", 1));
        var post = new ThreadPost(Guid.NewGuid(), catId, userId, false, "T", "B");
        post.SoftDelete();
        db.Context.ThreadPosts.Add(post);
        await db.Context.SaveChangesAsync();

        var result = await new GetThreadPostQueryHandler(db.Context).Handle(new GetThreadPostQuery(post.Id), default);
        Assert.Null(result);
    }
}
