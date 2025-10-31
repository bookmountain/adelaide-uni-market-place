using System.Collections.Generic;
using System.Linq;
using Application.Common.Interfaces;
using Contracts.DTO.Orders;
using Mapster;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Orders.Queries.GetMyOrders;

public sealed class GetMyOrdersQueryHandler : IRequestHandler<GetMyOrdersQuery, IReadOnlyCollection<OrderResponse>>
{
    private readonly IApplicationDbContext _dbContext;

    public GetMyOrdersQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<OrderResponse>> Handle(GetMyOrdersQuery request, CancellationToken cancellationToken)
    {
        var orders = await _dbContext.Orders
            .AsNoTracking()
            .Where(o => o.BuyerId == request.BuyerId)
            .Include(o => o.Items)
                .ThenInclude(oi => oi.Item)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(cancellationToken);

        return orders.Adapt<List<OrderResponse>>();
    }
}
