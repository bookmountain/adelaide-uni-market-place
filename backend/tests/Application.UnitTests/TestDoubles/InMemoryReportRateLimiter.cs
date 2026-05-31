using System.Collections.Concurrent;
using Application.Common.Interfaces;

namespace Application.UnitTests.TestDoubles;

public sealed class InMemoryReportRateLimiter : IReportRateLimiter
{
    private readonly ConcurrentDictionary<Guid, int> _counts = new();
    private readonly int _limit;
    public InMemoryReportRateLimiter(int limit = 10) => _limit = limit;

    public Task<bool> TryConsumeAsync(Guid userId, CancellationToken ct = default)
    {
        var count = _counts.AddOrUpdate(userId, 1, (_, c) => c + 1);
        return Task.FromResult(count <= _limit);
    }
}
