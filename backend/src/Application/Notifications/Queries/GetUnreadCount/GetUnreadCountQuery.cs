using Contracts.DTO.Notifications;
using MediatR;

namespace Application.Notifications.Queries.GetUnreadCount;

public sealed record GetUnreadCountQuery(Guid RecipientUserId) : IRequest<UnreadCountResponse>;
