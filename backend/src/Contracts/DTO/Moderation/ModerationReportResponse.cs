using Domain.Shared.Enums;

namespace Contracts.DTO.Moderation;

public sealed record ModerationReportResponse(
    Guid ReportId,
    ReportTargetType TargetType,
    Guid TargetId,
    ReportReason Reason,
    string? Notes,
    ReportStatus Status,
    bool TargetIsAnonymousToPublic,
    ModerationAuthor Author,
    string TargetExcerpt,
    DateTimeOffset CreatedAt);
