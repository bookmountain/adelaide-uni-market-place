using System.Collections.Concurrent;
using Application.Common.Interfaces;

namespace Application.UnitTests.TestDoubles;

public sealed class InMemoryRefreshTokenStore : IRefreshTokenStore
{
    private readonly ConcurrentDictionary<string, Guid> _tokens = new();

    public Task StoreAsync(Guid userId, string refreshToken, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        _tokens[refreshToken] = userId;
        return Task.CompletedTask;
    }

    public Task<Guid?> ValidateAsync(string refreshToken, CancellationToken cancellationToken = default)
        => Task.FromResult(_tokens.TryGetValue(refreshToken, out var userId) ? userId : (Guid?)null);

    public Task RevokeAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        _tokens.TryRemove(refreshToken, out _);
        return Task.CompletedTask;
    }

    public Task RevokeAllAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        foreach (var entry in _tokens.Where(kvp => kvp.Value == userId).ToList())
        {
            _tokens.TryRemove(entry.Key, out _);
        }

        return Task.CompletedTask;
    }
}
