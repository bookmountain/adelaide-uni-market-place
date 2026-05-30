namespace Application.Common.Interfaces;

public interface IRefreshTokenStore
{
    Task StoreAsync(Guid userId, string refreshToken, TimeSpan ttl, CancellationToken cancellationToken = default);

    /// <summary>Returns the owning user id if the token is valid, otherwise null.</summary>
    Task<Guid?> ValidateAsync(string refreshToken, CancellationToken cancellationToken = default);

    Task RevokeAsync(string refreshToken, CancellationToken cancellationToken = default);

    Task RevokeAllAsync(Guid userId, CancellationToken cancellationToken = default);
}
