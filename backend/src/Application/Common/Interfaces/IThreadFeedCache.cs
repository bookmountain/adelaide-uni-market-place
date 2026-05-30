using Application.Threads.Indexing;

namespace Application.Common.Interfaces;

public interface IThreadFeedCache
{
    Task<ThreadSearchPage?> GetAsync(string cacheKey, CancellationToken cancellationToken = default);
    Task SetAsync(string cacheKey, ThreadSearchPage page, CancellationToken cancellationToken = default);
    Task InvalidateAsync(string? categorySlug, CancellationToken cancellationToken = default);
}
