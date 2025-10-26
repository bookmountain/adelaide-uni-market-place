using Application.Common.Interfaces;
using Contracts.DTO.Items;
using Mapster;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Items.Queries;

public sealed class GetItemByIdQueryHandler : IRequestHandler<GetItemByIdQuery, ItemResponse?>
{
    private readonly IApplicationDbContext _dbContext;

    public GetItemByIdQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ItemResponse?> Handle(GetItemByIdQuery request, CancellationToken cancellationToken)
    {
        var item = await _dbContext.Items
            .AsNoTracking()
            .Where(i => i.Id == request.ItemId)
            .Include(i => i.Category)
            .Include(i => i.Images)
            .ProjectToType<ItemResponse>()
            .FirstOrDefaultAsync(cancellationToken);

        return item;
    }
}
