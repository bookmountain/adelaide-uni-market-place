using Contracts.DTO.Orders;
using MediatR;

namespace Application.Orders.Queries.GetMyOrders;

public sealed record GetMyOrdersQuery(Guid BuyerId) : IRequest<IReadOnlyCollection<OrderResponse>>;

