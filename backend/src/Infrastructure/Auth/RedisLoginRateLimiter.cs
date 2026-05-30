using Application.Common.Interfaces;
using Infrastructure.Configuration.Options;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Infrastructure.Auth;

public sealed class RedisLoginRateLimiter : ILoginRateLimiter
{
    private readonly IConnectionMultiplexer _redis;
    private readonly AuthOptions _options;

    public RedisLoginRateLimiter(IConnectionMultiplexer redis, IOptions<AuthOptions> options)
    {
        _redis = redis;
        _options = options.Value;
    }

    private static string EmailKey(string email) => $"auth:login-fail:email:{email.ToLowerInvariant()}";
    private static string IpKey(string ip) => $"auth:login-fail:ip:{ip}";

    public async Task<bool> IsBlockedAsync(string email, string ipAddress, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var emailCount = (int)await db.StringGetAsync(EmailKey(email));
        var ipCount = (int)await db.StringGetAsync(IpKey(ipAddress));
        return emailCount >= _options.LoginMaxFailuresPerEmail || ipCount >= _options.LoginMaxFailuresPerIp;
    }

    public async Task RecordFailureAsync(string email, string ipAddress, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var window = TimeSpan.FromMinutes(_options.LoginFailureWindowMinutes);
        await IncrementWithWindow(db, EmailKey(email), window);
        await IncrementWithWindow(db, IpKey(ipAddress), window);
    }

    public async Task ResetAsync(string email, string ipAddress, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        await db.KeyDeleteAsync(EmailKey(email));
        await db.KeyDeleteAsync(IpKey(ipAddress));
    }

    private static async Task IncrementWithWindow(IDatabase db, string key, TimeSpan window)
    {
        var count = await db.StringIncrementAsync(key);
        if (count == 1)
        {
            await db.KeyExpireAsync(key, window);
        }
    }
}
