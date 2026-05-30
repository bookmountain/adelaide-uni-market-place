namespace Application.Common.Interfaces;

public interface IIndexerIdempotencyStore
{
    Task<bool> HasProcessedAsync(string key, CancellationToken cancellationToken = default);
    Task MarkProcessedAsync(string key, CancellationToken cancellationToken = default);
}
