using Domain.Shared.Enums;

namespace Contracts.DTO.Notifications;

public sealed record NotificationResponse(
    Guid Id,
    NotificationType Type,
    Guid SourcePostId,
    Guid? SourceCommentId,
    NotificationActor Actor,
    bool IsRead,
    DateTimeOffset CreatedAt);

public sealed record NotificationListResponse(IReadOnlyList<NotificationResponse> Items, string? NextCursor);
