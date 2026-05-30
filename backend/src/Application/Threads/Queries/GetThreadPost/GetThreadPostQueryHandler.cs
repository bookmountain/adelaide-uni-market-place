using Application.Common.Interfaces;
using Application.Threads;
using Contracts.DTO.Threads;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Threads.Queries.GetThreadPost;

public sealed class GetThreadPostQueryHandler : IRequestHandler<GetThreadPostQuery, ThreadPostDetailResponse?>
{
    private readonly IApplicationDbContext _db;
    public GetThreadPostQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<ThreadPostDetailResponse?> Handle(GetThreadPostQuery request, CancellationToken ct)
    {
        var post = await _db.ThreadPosts
            .AsNoTracking()
            .Include(p => p.Author)
            .Include(p => p.Category)
            .Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.Id == request.PostId && !p.IsDeleted, ct);

        if (post is null || post.Author is null)
        {
            return null;
        }

        return new ThreadPostDetailResponse(
            post.Id,
            post.Category?.Slug ?? string.Empty,
            AuthorRefFactory.Create(post.IsAnonymous, post.Author),
            post.Title,
            post.Body,
            post.Images.OrderBy(i => i.Ordinal).Select(i => i.R2Key).ToList(),
            post.LikeCount,
            post.CommentCount,
            post.IsLocked,
            post.IsPinned,
            post.CreatedAt,
            post.LastActivityAt);
    }
}
