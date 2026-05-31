using Application.UnitTests.TestDoubles;
using Xunit;

namespace Application.UnitTests.Moderation;

public sealed class InMemoryReportRateLimiterTests
{
    [Fact]
    public async Task Allows_up_to_limit_then_blocks()
    {
        var limiter = new InMemoryReportRateLimiter(limit: 3);
        var user = Guid.NewGuid();

        Assert.True(await limiter.TryConsumeAsync(user));
        Assert.True(await limiter.TryConsumeAsync(user));
        Assert.True(await limiter.TryConsumeAsync(user));
        Assert.False(await limiter.TryConsumeAsync(user));
    }

    [Fact]
    public async Task Limits_are_per_user()
    {
        var limiter = new InMemoryReportRateLimiter(limit: 1);
        Assert.True(await limiter.TryConsumeAsync(Guid.NewGuid()));
        Assert.True(await limiter.TryConsumeAsync(Guid.NewGuid()));
    }
}
