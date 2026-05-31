namespace Contracts.DTO.Notifications;

public sealed record NotificationActor(bool IsAnonymous, string? Handle, Guid? UserId, string? DisplayName);
