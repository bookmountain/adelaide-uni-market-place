namespace Application.Common.Interfaces;

public interface ILoginRateLimiter
{
    Task<bool> IsBlockedAsync(string email, string ipAddress, CancellationToken cancellationToken = default);

    Task RecordFailureAsync(string email, string ipAddress, CancellationToken cancellationToken = default);

    Task ResetAsync(string email, string ipAddress, CancellationToken cancellationToken = default);
}
