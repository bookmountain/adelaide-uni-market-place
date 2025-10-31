using Application.Common.Interfaces;
using Contracts.DTO.Orders;
using Domain.Entities.Orders;
using Domain.Shared.Enums;
using Mapster;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Orders.Commands.CreateOrder;

public sealed class CreateOrderCommandHandler : IRequestHandler<CreateOrderCommand, OrderResponse>
{
    private readonly IApplicationDbContext _dbContext;

    public CreateOrderCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<OrderResponse> Handle(CreateOrderCommand request, CancellationToken cancellationToken)
    {
        var item = await _dbContext.Items
            .Include(i => i.Category)
            .FirstOrDefaultAsync(i => i.Id == request.ItemId, cancellationToken);

        if (item is null)
        {
            throw new InvalidOperationException("Item not found.");
        }

        if (item.Status != ItemStatus.Active)
        {
            throw new InvalidOperationException("Item is not available for ordering.");
        }

        if (item.SellerId == request.BuyerId)
        {
            throw new InvalidOperationException("You cannot order your own item.");
        }

        var now = DateTimeOffset.UtcNow;
        var orderId = Guid.NewGuid();

        var order = new Order(
            orderId,
            request.BuyerId,
            item.Price,
            OrderStatus.Pending,
            DeliveryMethod.InPerson,
            request.MeetingLocation,
            now,
            request.MeetingScheduledAt,
            PaymentProvider.None,
            paymentReference: null);

        var orderItem = new OrderItem(
            Guid.NewGuid(),
            orderId,
            item.Id,
            item.Price,
            quantity: 1);

        order.AddItem(orderItem);
        item.MarkSold();

        await _dbContext.Orders.AddAsync(order, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var response = order.Adapt<OrderResponse>();
        return response with
        {
            ItemId = item.Id,
            ItemTitle = item.Title,
            ItemPrice = item.Price
        };
    }
}
