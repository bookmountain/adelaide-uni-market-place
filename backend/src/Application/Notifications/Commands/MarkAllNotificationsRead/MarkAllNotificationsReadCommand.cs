using MediatR;

namespace Application.Notifications.Commands.MarkAllNotificationsRead;

public sealed record MarkAllNotificationsReadCommand(Guid RecipientUserId) : IRequest;
