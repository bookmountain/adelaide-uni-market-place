using Application.Common.Interfaces;
using StackExchange.Redis;

namespace Infrastructure.Caching;

public sealed class RedisReportRateLimiter : IReportRateLimiter
{
    private const int Limit = 10;
    private static readonly TimeSpan Window = TimeSpan.FromHours(1);
    private readonly IConnectionMultiplexer _redis;
    public RedisReportRateLimiter(IConnectionMultiplexer redis) => _redis = redis;

    public async Task<bool> TryConsumeAsync(Guid userId, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var key = $"ratelimit:report:{userId}";
        var count = await db.StringIncrementAsync(key);
        if (count == 1)
        {
            await db.KeyExpireAsync(key, Window);
        }
        return count <= Limit;
    }
}
