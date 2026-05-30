using Application.Common.Interfaces;
using StackExchange.Redis;

namespace Infrastructure.Caching;

public sealed class RedisIndexerIdempotencyStore : IIndexerIdempotencyStore
{
    private static readonly TimeSpan Retention = TimeSpan.FromHours(48);
    private readonly IConnectionMultiplexer _redis;
    public RedisIndexerIdempotencyStore(IConnectionMultiplexer redis) => _redis = redis;

    public Task<bool> TryMarkAsync(string key, CancellationToken cancellationToken = default)
        // SET key 1 NX EX 48h — returns true only if the key was newly set.
        => _redis.GetDatabase().StringSetAsync($"indexer:processed:{key}", "1", Retention, When.NotExists);
}
