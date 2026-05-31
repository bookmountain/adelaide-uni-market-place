using Application.Threads.Indexing;
using Application.UnitTests.Common;
using Domain.Entities.Threads;
using Domain.Entities.Users;
using Domain.Shared.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Application.UnitTests.Threads.Indexing;

public sealed class ThreadPostDocumentBuilderTests
{
    private static User NewUser(Guid id, string? anon)
    {
        var u = new User(id, "s@adelaide.edu.au", "Sarah", DateTimeOffset.UtcNow, "Student", "hash",
            AdelaideDepartment.ComputerScience, AcademicDegree.Bachelor, UserSex.Other, avatarUrl: "https://x/y.png", isActive: true);
        if (anon is not null) u.AssignAnonHandle(anon);
        return u;
    }

    [Fact]
    public async Task Builds_document_for_real_post_with_identity_and_hot_rank()
    {
        await using var db = await TestDb.CreateAsync();
        var userId = Guid.NewGuid(); var catId = Guid.NewGuid();
        db.Context.Users.Add(NewUser(userId, null));
        db.Context.ThreadCategories.Add(new ThreadCategory(catId, "general", "General", "x", "chat", 1));
        var post = new ThreadPost(Guid.NewGuid(), catId, userId, false, "Title", "Body");
        post.AdjustLikeCount(3);
        db.Context.ThreadPosts.Add(post);
        await db.Context.SaveChangesAsync();

        var doc = await new ThreadPostDocumentBuilder(db.Context).BuildAsync(post.Id, default);

        Assert.NotNull(doc);
        Assert.Equal(post.Id, doc!.PostId);
        Assert.Equal("general", doc.CategorySlug);
        Assert.False(doc.Author.IsAnonymous);
        Assert.Equal(userId, doc.Author.UserId);
        Assert.True(doc.HotRank > 0);
    }

    [Fact]
    public async Task Anonymous_post_document_hides_identity()
    {
        await using var db = await TestDb.CreateAsync();
        var userId = Guid.NewGuid(); var catId = Guid.NewGuid();
        db.Context.Users.Add(NewUser(userId, "quiet-koala-4821"));
        db.Context.ThreadCategories.Add(new ThreadCategory(catId, "general", "General", "x", "chat", 1));
        var post = new ThreadPost(Guid.NewGuid(), catId, userId, true, "T", "B");
        db.Context.ThreadPosts.Add(post);
        await db.Context.SaveChangesAsync();

        var doc = await new ThreadPostDocumentBuilder(db.Context).BuildAsync(post.Id, default);

        Assert.True(doc!.Author.IsAnonymous);
        Assert.Equal("quiet-koala-4821", doc.Author.Handle);
        Assert.Null(doc.Author.UserId);
        Assert.Null(doc.Author.DisplayName);
    }

    [Fact]
    public async Task Deleted_or_missing_post_returns_null()
    {
        await using var db = await TestDb.CreateAsync();
        var doc = await new ThreadPostDocumentBuilder(db.Context).BuildAsync(Guid.NewGuid(), default);
        Assert.Null(doc);
    }
}
