using Application.Threads.Indexing;

namespace Application.Common.Interfaces;

public interface IThreadSearchIndex
{
    Task UpsertAsync(ThreadPostDocument document, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid postId, CancellationToken cancellationToken = default);
    Task<ThreadSearchPage> SearchAsync(ThreadSearchQuery query, CancellationToken cancellationToken = default);
}
