using Contracts.DTO.Notifications;
using MediatR;

namespace Application.Notifications.Queries.GetNotifications;

public sealed record GetNotificationsQuery(Guid RecipientUserId, string? Cursor, int PageSize)
    : IRequest<NotificationListResponse>;
