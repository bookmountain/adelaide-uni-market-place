using Application.Threads.Indexing;
using Application.Threads.Queries.GetThreadFeed;
using Application.UnitTests.TestDoubles;
using Contracts.DTO.Threads;
using Xunit;

namespace Application.UnitTests.Threads;

public sealed class GetThreadFeedViaIndexTests
{
    private static ThreadPostDocument Doc(string title)
        => new(Guid.NewGuid(), "general", new AuthorRef(false, null, Guid.NewGuid(), "Sarah", null),
            title, "body", null, 0, 0, 0.5, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

    [Fact]
    public async Task Reads_from_search_index()
    {
        var idx = new InMemoryThreadSearchIndex();
        await idx.UpsertAsync(Doc("hello"));
        var handler = new GetThreadFeedQueryHandler(idx, new InMemoryThreadFeedCache());

        var feed = await handler.Handle(new GetThreadFeedQuery(null, "new", null, null, 10), default);

        Assert.Single(feed.Items);
        Assert.Equal("hello", feed.Items[0].Title);
    }

    [Fact]
    public async Task Hot_first_page_is_cached_and_served_from_cache()
    {
        var idx = new InMemoryThreadSearchIndex();
        await idx.UpsertAsync(Doc("one"));
        var cache = new InMemoryThreadFeedCache();
        var handler = new GetThreadFeedQueryHandler(idx, cache);

        var first = await handler.Handle(new GetThreadFeedQuery(null, "hot", null, null, 20), default);
        // Mutate the index; a cached hot first-page should NOT reflect the change.
        await idx.UpsertAsync(Doc("two"));
        var second = await handler.Handle(new GetThreadFeedQuery(null, "hot", null, null, 20), default);

        Assert.Equal(first.Items.Count, second.Items.Count); // served from cache
    }

    [Fact]
    public async Task Non_hot_or_paged_or_searched_requests_do_not_populate_the_hot_cache()
    {
        var idx = new InMemoryThreadSearchIndex();
        await idx.UpsertAsync(Doc("one"));
        var cache = new InMemoryThreadFeedCache();
        var handler = new GetThreadFeedQueryHandler(idx, cache);

        await handler.Handle(new GetThreadFeedQuery(null, "new", null, null, 20), default);   // not hot
        await handler.Handle(new GetThreadFeedQuery(null, "hot", null, "20", 20), default);    // paged
        await handler.Handle(new GetThreadFeedQuery(null, "hot", "bike", null, 20), default);  // text query

        // None of these are the canonical hot first-page, so the hot cache key stays empty.
        Assert.Null(await cache.GetAsync("threads:feed:all:hot"));
    }
}
