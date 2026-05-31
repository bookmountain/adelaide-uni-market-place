using Application.Common.Interfaces;
using StackExchange.Redis;

namespace Infrastructure.Caching;

public sealed class RedisIndexerIdempotencyStore : IIndexerIdempotencyStore
{
    private static readonly TimeSpan Retention = TimeSpan.FromHours(48);
    private readonly IConnectionMultiplexer _redis;
    public RedisIndexerIdempotencyStore(IConnectionMultiplexer redis) => _redis = redis;

    public Task<bool> HasProcessedAsync(string key, CancellationToken cancellationToken = default)
        => _redis.GetDatabase().KeyExistsAsync($"indexer:processed:{key}");

    public Task MarkProcessedAsync(string key, CancellationToken cancellationToken = default)
        => _redis.GetDatabase().StringSetAsync($"indexer:processed:{key}", "1", Retention);
}
