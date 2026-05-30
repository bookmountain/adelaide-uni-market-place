using Application.Common.Interfaces;
using Application.Threads;
using Contracts.DTO.Threads;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Threads.Queries.GetThreadComments;

public sealed class GetThreadCommentsQueryHandler
    : IRequestHandler<GetThreadCommentsQuery, IReadOnlyList<ThreadCommentResponse>>
{
    private readonly IApplicationDbContext _db;
    public GetThreadCommentsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<ThreadCommentResponse>> Handle(GetThreadCommentsQuery request, CancellationToken ct)
    {
        var comments = await _db.ThreadComments
            .AsNoTracking()
            .Include(c => c.Author)
            .Where(c => c.PostId == request.PostId)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync(ct);

        // Build a 2-level tree. A deleted comment is shown as a "[removed]" placeholder
        // only if it has surviving replies; otherwise it is omitted.
        ThreadCommentResponse Map(Domain.Entities.Threads.ThreadComment c, IReadOnlyList<ThreadCommentResponse> replies)
        {
            var author = AuthorRefFactory.Create(c.IsAnonymous, c.Author!);
            var body = c.IsDeleted ? "[removed]" : c.Body;
            return new ThreadCommentResponse(c.Id, c.ParentCommentId, author, body, c.LikeCount, c.IsDeleted, c.CreatedAt, replies);
        }

        var byParent = comments.Where(c => c.ParentCommentId is not null)
            .GroupBy(c => c.ParentCommentId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        var result = new List<ThreadCommentResponse>();
        foreach (var top in comments.Where(c => c.ParentCommentId is null))
        {
            var replies = byParent.TryGetValue(top.Id, out var kids)
                ? kids.Where(k => !k.IsDeleted).Select(k => Map(k, Array.Empty<ThreadCommentResponse>())).ToList()
                : new List<ThreadCommentResponse>();

            if (top.IsDeleted && replies.Count == 0)
            {
                continue; // drop fully-dead top-level comments
            }

            result.Add(Map(top, replies));
        }

        return result;
    }
}
