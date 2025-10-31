using Contracts.DTO.Orders;
using MediatR;

namespace Application.Orders.Queries.GetOrderById;

public sealed record GetOrderByIdQuery(Guid OrderId, Guid BuyerId) : IRequest<OrderResponse?>;

