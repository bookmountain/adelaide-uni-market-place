using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.Common.Interfaces;
using Contracts.DTO.Orders;
using Mapster;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Orders.Queries.GetOrderById;

public sealed class GetOrderByIdQueryHandler : IRequestHandler<GetOrderByIdQuery, OrderResponse?>
{
    private readonly IApplicationDbContext _dbContext;

    public GetOrderByIdQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<OrderResponse?> Handle(GetOrderByIdQuery request, CancellationToken cancellationToken)
    {
        var order = await _dbContext.Orders
            .AsNoTracking()
            .Where(o => o.Id == request.OrderId && o.BuyerId == request.BuyerId)
            .Include(o => o.Items)
                .ThenInclude(oi => oi.Item)
            .FirstOrDefaultAsync(cancellationToken);

        return order?.Adapt<OrderResponse>();
    }
}
