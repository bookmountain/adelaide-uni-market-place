using Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Application.Threads.Indexing;

public sealed class ThreadPostDocumentBuilder
{
    private readonly IApplicationDbContext _db;
    public ThreadPostDocumentBuilder(IApplicationDbContext db) => _db = db;

    public async Task<ThreadPostDocument?> BuildAsync(Guid postId, CancellationToken ct)
    {
        var post = await _db.ThreadPosts
            .AsNoTracking()
            .Include(p => p.Author)
            .Include(p => p.Category)
            .Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.Id == postId && !p.IsDeleted, ct);

        if (post is null || post.Author is null) return null;

        var hours = (DateTimeOffset.UtcNow - post.CreatedAt).TotalHours;
        var hotRank = (post.LikeCount + 2 * post.CommentCount) / Math.Pow(hours + 2, 1.8);

        return new ThreadPostDocument(
            post.Id,
            post.Category?.Slug ?? string.Empty,
            AuthorRefFactory.Create(post.IsAnonymous, post.Author),
            post.Title,
            post.Body.Length <= 200 ? post.Body : post.Body[..200],
            post.Images.OrderBy(i => i.Ordinal).Select(i => i.R2Key).FirstOrDefault(),
            post.LikeCount,
            post.CommentCount,
            hotRank,
            post.CreatedAt,
            post.LastActivityAt);
    }
}
