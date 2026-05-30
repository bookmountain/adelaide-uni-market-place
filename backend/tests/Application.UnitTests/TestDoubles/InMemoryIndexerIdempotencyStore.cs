using System.Collections.Concurrent;
using Application.Common.Interfaces;

namespace Application.UnitTests.TestDoubles;

public sealed class InMemoryIndexerIdempotencyStore : IIndexerIdempotencyStore
{
    private readonly ConcurrentDictionary<string, byte> _seen = new();
    public Task<bool> TryMarkAsync(string key, CancellationToken ct = default)
        => Task.FromResult(_seen.TryAdd(key, 1));
}
