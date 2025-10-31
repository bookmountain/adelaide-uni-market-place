using Contracts.DTO.Orders;
using MediatR;

namespace Application.Orders.Commands.CreateOrder;

public sealed record CreateOrderCommand(
    Guid BuyerId,
    Guid ItemId,
    string MeetingLocation,
    DateTimeOffset? MeetingScheduledAt) : IRequest<OrderResponse>;
