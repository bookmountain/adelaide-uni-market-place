using Application.Common.Interfaces;
using Application.Threads.Indexing;
using Contracts.DTO.Threads;
using MediatR;

namespace Application.Threads.Queries.GetThreadFeed;

public sealed class GetThreadFeedQueryHandler : IRequestHandler<GetThreadFeedQuery, ThreadFeedResponse>
{
    private readonly IThreadSearchIndex _index;
    private readonly IThreadFeedCache _cache;

    public GetThreadFeedQueryHandler(IThreadSearchIndex index, IThreadFeedCache cache)
    {
        _index = index;
        _cache = cache;
    }

    public async Task<ThreadFeedResponse> Handle(GetThreadFeedQuery request, CancellationToken ct)
    {
        var sort = string.IsNullOrWhiteSpace(request.Sort) ? "hot" : request.Sort.ToLowerInvariant();
        var query = new ThreadSearchQuery(request.CategorySlug, sort, request.Query, request.Cursor, request.PageSize);

        // Cache only the canonical hot first-page (no text query, no cursor) — the highest-traffic view.
        var cacheable = sort == "hot" && string.IsNullOrWhiteSpace(request.Query) && string.IsNullOrWhiteSpace(request.Cursor);
        var cacheKey = $"threads:feed:{request.CategorySlug ?? "all"}:hot";

        if (cacheable)
        {
            var cached = await _cache.GetAsync(cacheKey, ct);
            if (cached is not null) return new ThreadFeedResponse(cached.Items, cached.NextCursor);
        }

        var page = await _index.SearchAsync(query, ct);

        if (cacheable)
        {
            await _cache.SetAsync(cacheKey, page, ct);
        }

        return new ThreadFeedResponse(page.Items, page.NextCursor);
    }
}
