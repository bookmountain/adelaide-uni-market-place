using Application.Common.Interfaces;
using Application.Threads;
using Contracts.DTO.Threads;
using Domain.Entities.Threads;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Threads.Queries.GetThreadFeed;

/// <summary>
/// PROVISIONAL Postgres feed. Plan 3 replaces this with an Elasticsearch read model
/// (search_after cursor + precomputed hot_rank). Keep the handler self-contained.
/// </summary>
public sealed class GetThreadFeedQueryHandler : IRequestHandler<GetThreadFeedQuery, ThreadFeedResponse>
{
    private const int CandidateWindow = 500;
    private readonly IApplicationDbContext _db;
    public GetThreadFeedQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<ThreadFeedResponse> Handle(GetThreadFeedQuery request, CancellationToken ct)
    {
        var pageSize = Math.Clamp(request.PageSize <= 0 ? 20 : request.PageSize, 1, 50);
        var offset = ParseCursor(request.Cursor);

        var query = _db.ThreadPosts
            .AsNoTracking()
            .Include(p => p.Author)
            .Include(p => p.Category)
            .Include(p => p.Images)
            .Where(p => !p.IsDeleted);

        if (!string.IsNullOrWhiteSpace(request.CategorySlug))
        {
            var slug = request.CategorySlug.ToLowerInvariant();
            query = query.Where(p => p.Category!.Slug == slug);
        }

        // Bounded candidate set — order by LastActivityAt in memory to avoid SQLite DateTimeOffset quirks.
        // (Postgres in production supports DateTimeOffset ORDER BY natively; Plan 3 replaces this entirely.)
        var candidates = (await query.ToListAsync(ct))
            .OrderByDescending(p => p.LastActivityAt)
            .Take(CandidateWindow)
            .ToList();

        IEnumerable<ThreadPost> ordered = request.Sort?.ToLowerInvariant() switch
        {
            "top" => candidates.OrderByDescending(p => p.LikeCount).ThenByDescending(p => p.CreatedAt),
            "hot" => candidates.OrderByDescending(HotScore),
            _ => candidates.OrderByDescending(p => p.CreatedAt) // "new"
        };

        var orderedList = ordered.ToList();
        var page = orderedList.Skip(offset).Take(pageSize).ToList();
        var next = offset + pageSize < orderedList.Count ? (offset + pageSize).ToString() : null;

        var items = page.Select(p => new ThreadPostSummary(
            p.Id,
            p.Category?.Slug ?? string.Empty,
            AuthorRefFactory.Create(p.IsAnonymous, p.Author!),
            p.Title,
            Excerpt(p.Body),
            p.Images.OrderBy(i => i.Ordinal).Select(i => i.R2Key).FirstOrDefault(),
            p.LikeCount,
            p.CommentCount,
            p.CreatedAt,
            p.LastActivityAt)).ToList();

        return new ThreadFeedResponse(items, next);
    }

    private static double HotScore(ThreadPost p)
    {
        var hours = (DateTimeOffset.UtcNow - p.CreatedAt).TotalHours;
        return (p.LikeCount + 2 * p.CommentCount) / Math.Pow(hours + 2, 1.8);
    }

    private static string Excerpt(string body) => body.Length <= 200 ? body : body[..200];

    private static int ParseCursor(string? cursor)
        => int.TryParse(cursor, out var n) && n >= 0 ? n : 0;
}
