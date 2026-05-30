using Application.UnitTests.Common;
using Application.UnitTests.TestDoubles;
using Domain.Entities.Threads;
using Domain.Entities.Users;
using Domain.Shared.Enums;
using Infrastructure.Events;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Application.UnitTests.Threads.Indexing;

public sealed class ThreadIndexingConsumerTests
{
    private static async Task<(Infrastructure.Data.MarketplaceDbContext db, ThreadPost post)> SeedPost(TestDb t, bool deleted = false)
    {
        var userId = Guid.NewGuid(); var catId = Guid.NewGuid();
        t.Context.Users.Add(new User(userId, "s@adelaide.edu.au", "Sarah", DateTimeOffset.UtcNow, "Student", "hash",
            AdelaideDepartment.ComputerScience, AcademicDegree.Bachelor, UserSex.Other, isActive: true));
        t.Context.ThreadCategories.Add(new ThreadCategory(catId, "general", "General", "x", "chat", 1));
        var post = new ThreadPost(Guid.NewGuid(), catId, userId, false, "T", "B");
        if (deleted) post.SoftDelete();
        t.Context.ThreadPosts.Add(post);
        await t.Context.SaveChangesAsync();
        return (t.Context, post);
    }

    private static ThreadIndexingService NewService(Infrastructure.Data.MarketplaceDbContext db, InMemoryThreadSearchIndex idx, InMemoryThreadFeedCache cache)
        => new(new Application.Threads.Indexing.ThreadPostDocumentBuilder(db), idx, cache, new InMemoryIndexerIdempotencyStore());

    [Fact]
    public async Task Indexes_post_on_created_and_invalidates_cache()
    {
        await using var t = await TestDb.CreateAsync();
        var (db, post) = await SeedPost(t);
        var idx = new InMemoryThreadSearchIndex(); var cache = new InMemoryThreadFeedCache();
        var svc = NewService(db, idx, cache);

        await svc.HandlePostChangedAsync(post.Id, "evt-1", default);

        var page = await idx.SearchAsync(new Application.Threads.Indexing.ThreadSearchQuery(null, "new", null, null, 10));
        Assert.Single(page.Items);
        Assert.True(cache.InvalidateCalls >= 1);
    }

    [Fact]
    public async Task Soft_deleted_post_is_removed_from_index()
    {
        await using var t = await TestDb.CreateAsync();
        var (db, post) = await SeedPost(t);
        var idx = new InMemoryThreadSearchIndex(); var cache = new InMemoryThreadFeedCache();
        var svc = NewService(db, idx, cache);
        await svc.HandlePostChangedAsync(post.Id, "evt-1", default);

        // Now soft-delete and signal a delete.
        (await db.ThreadPosts.FirstAsync(p => p.Id == post.Id)).SoftDelete();
        await db.SaveChangesAsync();
        await svc.HandlePostDeletedAsync(post.Id, "evt-2", default);

        var page = await idx.SearchAsync(new Application.Threads.Indexing.ThreadSearchQuery(null, "new", null, null, 10));
        Assert.Empty(page.Items);
    }

    [Fact]
    public async Task Duplicate_event_id_is_skipped()
    {
        await using var t = await TestDb.CreateAsync();
        var (db, post) = await SeedPost(t);
        var idx = new InMemoryThreadSearchIndex(); var cache = new InMemoryThreadFeedCache();
        var idem = new InMemoryIndexerIdempotencyStore();
        var svc = new ThreadIndexingService(new Application.Threads.Indexing.ThreadPostDocumentBuilder(db), idx, cache, idem);

        await svc.HandlePostChangedAsync(post.Id, "same-key", default);
        var firstInvalidations = cache.InvalidateCalls;
        await svc.HandlePostChangedAsync(post.Id, "same-key", default); // duplicate

        Assert.Equal(firstInvalidations, cache.InvalidateCalls); // no extra work
    }
}
