namespace Contracts.Events;

public sealed record ItemCreatedEvent(
    Guid ItemId,
    Guid SellerId,
    Guid CategoryId,
    decimal Price,
    DateTimeOffset CreatedAt);