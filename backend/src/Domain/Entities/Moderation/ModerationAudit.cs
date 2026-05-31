using Domain.Shared.Enums;

namespace Domain.Entities.Moderation;

public class ModerationAudit
{
    private ModerationAudit() { }

    private ModerationAudit(Guid id, Guid adminUserId, ReportTargetType targetType, Guid targetId, string action, string? reason)
    {
        Id = id;
        AdminUserId = adminUserId;
        TargetType = targetType;
        TargetId = targetId;
        Action = action;
        Reason = reason;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid AdminUserId { get; private set; }
    public ReportTargetType TargetType { get; private set; }
    public Guid TargetId { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public string? Reason { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static ModerationAudit Record(Guid adminUserId, ReportTargetType targetType, Guid targetId, string action, string? reason)
        => new(Guid.NewGuid(), adminUserId, targetType, targetId, action, reason);
}
