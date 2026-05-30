using System.Collections.Concurrent;
using Application.Common.Interfaces;

namespace Application.UnitTests.TestDoubles;

public sealed class InMemoryIndexerIdempotencyStore : IIndexerIdempotencyStore
{
    private readonly ConcurrentDictionary<string, byte> _seen = new();
    public Task<bool> HasProcessedAsync(string key, CancellationToken ct = default) => Task.FromResult(_seen.ContainsKey(key));
    public Task MarkProcessedAsync(string key, CancellationToken ct = default) { _seen[key] = 1; return Task.CompletedTask; }
}
