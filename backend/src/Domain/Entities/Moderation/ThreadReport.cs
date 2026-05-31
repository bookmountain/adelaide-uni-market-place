using Domain.Shared.Enums;

namespace Domain.Entities.Moderation;

public class ThreadReport
{
    private ThreadReport() { }

    public ThreadReport(Guid id, Guid reporterUserId, ReportTargetType targetType, Guid targetId, ReportReason reason, string? notes)
    {
        Id = id;
        ReporterUserId = reporterUserId;
        TargetType = targetType;
        TargetId = targetId;
        Reason = reason;
        Notes = notes;
        Status = ReportStatus.Open;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid ReporterUserId { get; private set; }
    public ReportTargetType TargetType { get; private set; }
    public Guid TargetId { get; private set; }
    public ReportReason Reason { get; private set; }
    public string? Notes { get; private set; }
    public ReportStatus Status { get; private set; }
    public Guid? ReviewedByUserId { get; private set; }
    public DateTimeOffset? ReviewedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public void Resolve(Guid adminUserId, ReportStatus status)
    {
        Status = status;
        ReviewedByUserId = adminUserId;
        ReviewedAt = DateTimeOffset.UtcNow;
    }
}
