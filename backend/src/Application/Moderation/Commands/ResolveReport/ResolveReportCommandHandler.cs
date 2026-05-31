using Application.Common.Interfaces;
using Contracts.Events.Threads;
using Domain.Entities.Moderation;
using Domain.Shared.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Moderation.Commands.ResolveReport;

public sealed class ResolveReportCommandHandler : IRequestHandler<ResolveReportCommand>
{
    private readonly IApplicationDbContext _db;
    private readonly IOutbox _outbox;

    public ResolveReportCommandHandler(IApplicationDbContext db, IOutbox outbox)
    {
        _db = db;
        _outbox = outbox;
    }

    public async Task Handle(ResolveReportCommand request, CancellationToken ct)
    {
        var report = await _db.ThreadReports.FirstOrDefaultAsync(r => r.Id == request.ReportId, ct)
            ?? throw new InvalidOperationException("Report not found.");

        var action = request.Action.Trim().ToLowerInvariant();
        switch (action)
        {
            case "dismiss":
                report.Resolve(request.AdminUserId, ReportStatus.Dismissed);
                break;

            case "warn-user":
                report.Resolve(request.AdminUserId, ReportStatus.Reviewed);
                break;

            case "remove-content":
                await RemoveContentAsync(report, ct);
                report.Resolve(request.AdminUserId, ReportStatus.Reviewed);
                break;

            default:
                throw new InvalidOperationException($"Unknown moderation action '{request.Action}'.");
        }

        _db.ModerationAudits.Add(ModerationAudit.Record(request.AdminUserId, report.TargetType, report.TargetId, action, report.Reason.ToString()));
        await _db.SaveChangesAsync(ct);
    }

    private async Task RemoveContentAsync(ThreadReport report, CancellationToken ct)
    {
        if (report.TargetType == ReportTargetType.Post)
        {
            var post = await _db.ThreadPosts.FirstOrDefaultAsync(p => p.Id == report.TargetId, ct);
            if (post is not null && !post.IsDeleted)
            {
                post.SoftDelete();
                _outbox.Enqueue(ThreadEventTypes.PostDeleted, new ThreadPostDeleted(post.Id));
            }
        }
        else
        {
            var comment = await _db.ThreadComments.FirstOrDefaultAsync(c => c.Id == report.TargetId, ct);
            if (comment is not null && !comment.IsDeleted)
            {
                comment.SoftDelete();
                _outbox.Enqueue(ThreadEventTypes.CommentDeleted, new ThreadCommentDeleted(comment.PostId, comment.Id));
            }
        }
    }
}
