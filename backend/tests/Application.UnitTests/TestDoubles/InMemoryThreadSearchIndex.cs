using System.Collections.Concurrent;
using Application.Common.Interfaces;
using Application.Threads.Indexing;
using Contracts.DTO.Threads;

namespace Application.UnitTests.TestDoubles;

public sealed class InMemoryThreadSearchIndex : IThreadSearchIndex
{
    private readonly ConcurrentDictionary<Guid, ThreadPostDocument> _docs = new();

    public Task UpsertAsync(ThreadPostDocument document, CancellationToken ct = default)
    {
        _docs[document.PostId] = document;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid postId, CancellationToken ct = default)
    {
        _docs.TryRemove(postId, out _);
        return Task.CompletedTask;
    }

    public Task<ThreadSearchPage> SearchAsync(ThreadSearchQuery query, CancellationToken ct = default)
    {
        IEnumerable<ThreadPostDocument> q = _docs.Values;

        if (!string.IsNullOrWhiteSpace(query.CategorySlug))
            q = q.Where(d => d.CategorySlug == query.CategorySlug);

        if (!string.IsNullOrWhiteSpace(query.Query))
        {
            var term = query.Query.Trim();
            q = q.Where(d => d.Title.Contains(term, StringComparison.OrdinalIgnoreCase)
                          || d.Body.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        q = query.Sort?.ToLowerInvariant() switch
        {
            "top" => q.OrderByDescending(d => d.LikeCount).ThenByDescending(d => d.CreatedAt),
            "hot" => q.OrderByDescending(d => d.HotRank).ThenByDescending(d => d.CreatedAt),
            _ => q.OrderByDescending(d => d.CreatedAt)
        };

        var ordered = q.ToList();
        var pageSize = Math.Clamp(query.PageSize <= 0 ? 20 : query.PageSize, 1, 50);
        var offset = int.TryParse(query.Cursor, out var n) && n >= 0 ? n : 0;
        var slice = ordered.Skip(offset).Take(pageSize).ToList();
        var next = offset + pageSize < ordered.Count ? (offset + pageSize).ToString() : null;

        var items = slice.Select(d => new ThreadPostSummary(
            d.PostId, d.CategorySlug, d.Author, d.Title, d.Body, d.ThumbnailKey,
            d.LikeCount, d.CommentCount, d.CreatedAt, d.LastActivityAt)).ToList();

        return Task.FromResult(new ThreadSearchPage(items, next));
    }
}
