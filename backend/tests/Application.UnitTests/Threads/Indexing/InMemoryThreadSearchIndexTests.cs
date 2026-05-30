using Application.Threads.Indexing;
using Application.UnitTests.TestDoubles;
using Contracts.DTO.Threads;
using Xunit;

namespace Application.UnitTests.Threads.Indexing;

public sealed class InMemoryThreadSearchIndexTests
{
    private static ThreadPostDocument Doc(string title, string slug, int likes, int comments, double hot, DateTimeOffset created)
        => new(Guid.NewGuid(), slug, new AuthorRef(false, null, Guid.NewGuid(), "Sarah", null),
            title, "body", null, likes, comments, hot, created, created);

    [Fact]
    public async Task Upsert_then_search_new_returns_most_recent_first()
    {
        var idx = new InMemoryThreadSearchIndex();
        var older = Doc("older", "general", 0, 0, 0.1, DateTimeOffset.UtcNow.AddMinutes(-10));
        var newer = Doc("newer", "general", 0, 0, 0.1, DateTimeOffset.UtcNow);
        await idx.UpsertAsync(older); await idx.UpsertAsync(newer);

        var page = await idx.SearchAsync(new ThreadSearchQuery(null, "new", null, null, 10));
        Assert.Equal("newer", page.Items[0].Title);
    }

    [Fact]
    public async Task Search_top_orders_by_likes_and_filters_category()
    {
        var idx = new InMemoryThreadSearchIndex();
        await idx.UpsertAsync(Doc("a", "general", 1, 0, 0.1, DateTimeOffset.UtcNow));
        await idx.UpsertAsync(Doc("b", "general", 9, 0, 0.1, DateTimeOffset.UtcNow));
        await idx.UpsertAsync(Doc("other", "rides", 99, 0, 0.1, DateTimeOffset.UtcNow));

        var page = await idx.SearchAsync(new ThreadSearchQuery("general", "top", null, null, 10));
        Assert.Equal(2, page.Items.Count);
        Assert.Equal("b", page.Items[0].Title);
    }

    [Fact]
    public async Task Delete_removes_from_results_and_cursor_paginates()
    {
        var idx = new InMemoryThreadSearchIndex();
        var d1 = Doc("p0", "general", 0, 0, 0.1, DateTimeOffset.UtcNow.AddMinutes(-3));
        var d2 = Doc("p1", "general", 0, 0, 0.1, DateTimeOffset.UtcNow.AddMinutes(-2));
        var d3 = Doc("p2", "general", 0, 0, 0.1, DateTimeOffset.UtcNow.AddMinutes(-1));
        await idx.UpsertAsync(d1); await idx.UpsertAsync(d2); await idx.UpsertAsync(d3);
        await idx.DeleteAsync(d2.PostId);

        var page1 = await idx.SearchAsync(new ThreadSearchQuery(null, "new", null, null, 1));
        Assert.Single(page1.Items);
        Assert.Equal("p2", page1.Items[0].Title);
        Assert.NotNull(page1.NextCursor);

        var page2 = await idx.SearchAsync(new ThreadSearchQuery(null, "new", null, page1.NextCursor, 1));
        Assert.Equal("p0", page2.Items[0].Title);
        Assert.Null(page2.NextCursor);
    }

    [Fact]
    public async Task Text_query_matches_title_or_body()
    {
        var idx = new InMemoryThreadSearchIndex();
        await idx.UpsertAsync(Doc("CHEM1100 textbook", "textbooks", 0, 0, 0.1, DateTimeOffset.UtcNow));
        await idx.UpsertAsync(Doc("bike for sale", "general", 0, 0, 0.1, DateTimeOffset.UtcNow));

        var page = await idx.SearchAsync(new ThreadSearchQuery(null, "new", "textbook", null, 10));
        Assert.Single(page.Items);
        Assert.Contains("CHEM1100", page.Items[0].Title);
    }
}
