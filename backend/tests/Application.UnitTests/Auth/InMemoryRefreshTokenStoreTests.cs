using Application.UnitTests.TestDoubles;
using Xunit;

namespace Application.UnitTests.Auth;

public sealed class InMemoryRefreshTokenStoreTests
{
    [Fact]
    public async Task Stored_token_validates_to_its_user()
    {
        var store = new InMemoryRefreshTokenStore();
        var userId = Guid.NewGuid();
        await store.StoreAsync(userId, "tok-1", TimeSpan.FromDays(14));

        Assert.Equal(userId, await store.ValidateAsync("tok-1"));
    }

    [Fact]
    public async Task Revoked_token_no_longer_validates()
    {
        var store = new InMemoryRefreshTokenStore();
        var userId = Guid.NewGuid();
        await store.StoreAsync(userId, "tok-1", TimeSpan.FromDays(14));

        await store.RevokeAsync("tok-1");

        Assert.Null(await store.ValidateAsync("tok-1"));
    }

    [Fact]
    public async Task RevokeAll_invalidates_every_token_for_user()
    {
        var store = new InMemoryRefreshTokenStore();
        var userId = Guid.NewGuid();
        await store.StoreAsync(userId, "tok-1", TimeSpan.FromDays(14));
        await store.StoreAsync(userId, "tok-2", TimeSpan.FromDays(14));

        await store.RevokeAllAsync(userId);

        Assert.Null(await store.ValidateAsync("tok-1"));
        Assert.Null(await store.ValidateAsync("tok-2"));
    }
}
