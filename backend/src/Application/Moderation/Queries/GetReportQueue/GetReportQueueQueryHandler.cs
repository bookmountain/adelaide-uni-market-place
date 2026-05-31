using Application.Common.Interfaces;
using Contracts.DTO.Moderation;
using Domain.Shared.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Moderation.Queries.GetReportQueue;

public sealed class GetReportQueueQueryHandler : IRequestHandler<GetReportQueueQuery, IReadOnlyList<ModerationReportResponse>>
{
    private readonly IApplicationDbContext _db;
    public GetReportQueueQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<ModerationReportResponse>> Handle(GetReportQueueQuery request, CancellationToken ct)
    {
        var reports = (await _db.ThreadReports
            .AsNoTracking()
            .Where(r => r.Status == request.Status)
            .ToListAsync(ct))
            .OrderBy(r => r.CreatedAt)
            .ToList();

        var result = new List<ModerationReportResponse>(reports.Count);
        foreach (var r in reports)
        {
            bool anon;
            string excerpt;
            Guid authorId;

            if (r.TargetType == ReportTargetType.Post)
            {
                var post = await _db.ThreadPosts.AsNoTracking().FirstOrDefaultAsync(p => p.Id == r.TargetId, ct);
                anon = post?.IsAnonymous ?? false;
                excerpt = post is null ? "[deleted]" : Excerpt(post.Title);
                authorId = post?.AuthorUserId ?? Guid.Empty;
            }
            else
            {
                var comment = await _db.ThreadComments.AsNoTracking().FirstOrDefaultAsync(c => c.Id == r.TargetId, ct);
                anon = comment?.IsAnonymous ?? false;
                excerpt = comment is null ? "[deleted]" : Excerpt(comment.Body);
                authorId = comment?.AuthorUserId ?? Guid.Empty;
            }

            // The anon-break: resolve the REAL author identity for moderators.
            var author = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == authorId, ct);
            var moderationAuthor = new ModerationAuthor(authorId, author?.DisplayName ?? "[unknown]");

            result.Add(new ModerationReportResponse(
                r.Id, r.TargetType, r.TargetId, r.Reason, r.Notes, r.Status, anon, moderationAuthor, excerpt, r.CreatedAt));
        }

        return result;
    }

    private static string Excerpt(string text) => text.Length <= 200 ? text : text[..200];
}
