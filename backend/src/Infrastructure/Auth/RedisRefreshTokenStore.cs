using Application.Common.Interfaces;
using StackExchange.Redis;

namespace Infrastructure.Auth;

public sealed class RedisRefreshTokenStore : IRefreshTokenStore
{
    private readonly IConnectionMultiplexer _redis;

    public RedisRefreshTokenStore(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    private static string TokenKey(string token) => $"auth:refresh:{token}";
    private static string UserSetKey(Guid userId) => $"auth:refresh-by-user:{userId}";

    public async Task StoreAsync(Guid userId, string refreshToken, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        await db.StringSetAsync(TokenKey(refreshToken), userId.ToString(), ttl);
        await db.SetAddAsync(UserSetKey(userId), refreshToken);
        await db.KeyExpireAsync(UserSetKey(userId), ttl);
    }

    public async Task<Guid?> ValidateAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var value = await _redis.GetDatabase().StringGetAsync(TokenKey(refreshToken));
        return value.HasValue && Guid.TryParse(value!, out var userId) ? userId : null;
    }

    public async Task RevokeAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var value = await db.StringGetAsync(TokenKey(refreshToken));
        await db.KeyDeleteAsync(TokenKey(refreshToken));
        if (value.HasValue && Guid.TryParse(value!, out var userId))
        {
            await db.SetRemoveAsync(UserSetKey(userId), refreshToken);
        }
    }

    public async Task RevokeAllAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var tokens = await db.SetMembersAsync(UserSetKey(userId));
        foreach (var token in tokens)
        {
            await db.KeyDeleteAsync(TokenKey(token!));
        }

        await db.KeyDeleteAsync(UserSetKey(userId));
    }
}
