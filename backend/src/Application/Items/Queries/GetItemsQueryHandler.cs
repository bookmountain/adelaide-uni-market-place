using Application.Common.Interfaces;
using Contracts.DTO.Items;
using Mapster;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Items.Queries;

public sealed class GetItemsQueryHandler : IRequestHandler<GetItemsQuery, ListItemsResponse>
{
    private readonly IApplicationDbContext _dbContext;

    public GetItemsQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ListItemsResponse> Handle(GetItemsQuery request, CancellationToken cancellationToken)
    {
        var items = await _dbContext.Items
            .AsNoTracking()
            .Include(i => i.Category)
            .Include(i => i.Images)
            .OrderByDescending(i => i.CreatedAt)
            .ProjectToType<ItemResponse>()
            .ToListAsync(cancellationToken);

        return new ListItemsResponse(items);
    }
}
