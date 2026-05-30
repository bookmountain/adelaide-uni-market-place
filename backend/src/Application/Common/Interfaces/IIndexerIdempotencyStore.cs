namespace Application.Common.Interfaces;

public interface IIndexerIdempotencyStore
{
    /// <summary>Returns true if this is the first time the key is seen (and records it); false if already processed.</summary>
    Task<bool> TryMarkAsync(string key, CancellationToken cancellationToken = default);
}
