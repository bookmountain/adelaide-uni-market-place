using MediatR;

namespace Application.Notifications.Commands.MarkNotificationRead;

public sealed record MarkNotificationReadCommand(Guid RecipientUserId, Guid NotificationId) : IRequest;
