using Application.Common.Interfaces;
using Domain.Entities.Moderation;
using Domain.Shared.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Moderation.Commands.CreateReport;

public sealed class CreateReportCommandHandler : IRequestHandler<CreateReportCommand, Guid>
{
    private readonly IApplicationDbContext _db;
    private readonly IReportRateLimiter _rateLimiter;

    public CreateReportCommandHandler(IApplicationDbContext db, IReportRateLimiter rateLimiter)
    {
        _db = db;
        _rateLimiter = rateLimiter;
    }

    public async Task<Guid> Handle(CreateReportCommand request, CancellationToken ct)
    {
        if (!await _rateLimiter.TryConsumeAsync(request.ReporterUserId, ct))
        {
            throw new InvalidOperationException("You have filed too many reports recently. Please try again later.");
        }

        var exists = request.TargetType == ReportTargetType.Post
            ? await _db.ThreadPosts.AnyAsync(p => p.Id == request.TargetId && !p.IsDeleted, ct)
            : await _db.ThreadComments.AnyAsync(c => c.Id == request.TargetId && !c.IsDeleted, ct);
        if (!exists)
        {
            throw new InvalidOperationException("The reported content no longer exists.");
        }

        var report = new ThreadReport(Guid.NewGuid(), request.ReporterUserId, request.TargetType, request.TargetId, request.Reason, request.Notes);
        _db.ThreadReports.Add(report);
        await _db.SaveChangesAsync(ct);
        return report.Id;
    }
}
