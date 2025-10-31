namespace Contracts.DTO.Orders;

public sealed record OrderResponse(
    Guid OrderId,
    Guid ItemId,
    string ItemTitle,
    decimal ItemPrice,
    string MeetingLocation,
    DateTimeOffset? MeetingScheduledAt,
    string DeliveryMethod,
    string Status,
    DateTimeOffset CreatedAt);

