using System.Text.Json;
using Application.Common.Interfaces;
using Application.Threads.Indexing;
using StackExchange.Redis;

namespace Infrastructure.Caching;

public sealed class RedisThreadFeedCache : IThreadFeedCache
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);
    private readonly IConnectionMultiplexer _redis;
    public RedisThreadFeedCache(IConnectionMultiplexer redis) => _redis = redis;

    public async Task<ThreadSearchPage?> GetAsync(string cacheKey, CancellationToken cancellationToken = default)
    {
        var value = await _redis.GetDatabase().StringGetAsync(cacheKey);
        return value.HasValue ? JsonSerializer.Deserialize<ThreadSearchPage>(value!) : null;
    }

    public Task SetAsync(string cacheKey, ThreadSearchPage page, CancellationToken cancellationToken = default)
        => _redis.GetDatabase().StringSetAsync(cacheKey, JsonSerializer.Serialize(page), Ttl);

    public async Task InvalidateAsync(string? categorySlug, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        // Invalidate the global hot page always, and the affected category's hot page when known.
        await db.KeyDeleteAsync("threads:feed:all:hot");
        if (!string.IsNullOrWhiteSpace(categorySlug))
        {
            await db.KeyDeleteAsync($"threads:feed:{categorySlug}:hot");
        }
    }
}
