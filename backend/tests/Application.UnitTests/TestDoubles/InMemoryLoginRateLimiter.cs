using System.Collections.Concurrent;
using Application.Common.Interfaces;

namespace Application.UnitTests.TestDoubles;

public sealed class InMemoryLoginRateLimiter : ILoginRateLimiter
{
    private readonly ConcurrentDictionary<string, int> _failures = new();
    private readonly int _threshold;

    public InMemoryLoginRateLimiter(int threshold = 5) => _threshold = threshold;

    public Task<bool> IsBlockedAsync(string email, string ipAddress, CancellationToken cancellationToken = default)
        => Task.FromResult(_failures.GetValueOrDefault(email) >= _threshold);

    public Task RecordFailureAsync(string email, string ipAddress, CancellationToken cancellationToken = default)
    {
        _failures.AddOrUpdate(email, 1, (_, count) => count + 1);
        return Task.CompletedTask;
    }

    public Task ResetAsync(string email, string ipAddress, CancellationToken cancellationToken = default)
    {
        _failures.TryRemove(email, out _);
        return Task.CompletedTask;
    }
}
