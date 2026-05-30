using System.Collections.Concurrent;
using Application.Common.Interfaces;
using Application.Threads.Indexing;

namespace Application.UnitTests.TestDoubles;

public sealed class InMemoryThreadFeedCache : IThreadFeedCache
{
    private readonly ConcurrentDictionary<string, ThreadSearchPage> _cache = new();
    public int InvalidateCalls { get; private set; }

    public Task<ThreadSearchPage?> GetAsync(string cacheKey, CancellationToken ct = default)
        => Task.FromResult(_cache.TryGetValue(cacheKey, out var p) ? p : null);

    public Task SetAsync(string cacheKey, ThreadSearchPage page, CancellationToken ct = default)
    {
        _cache[cacheKey] = page;
        return Task.CompletedTask;
    }

    public Task InvalidateAsync(string? categorySlug, CancellationToken ct = default)
    {
        InvalidateCalls++;
        _cache.Clear();
        return Task.CompletedTask;
    }
}
