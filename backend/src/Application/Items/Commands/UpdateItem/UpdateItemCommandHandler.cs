using Application.Common.Interfaces;
using Contracts.DTO.Items;
using Domain.Shared.Enums;
using Mapster;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Items.Commands.UpdateItem;

public sealed class UpdateItemCommandHandler : IRequestHandler<UpdateItemCommand, ItemResponse>
{
    private readonly IApplicationDbContext _dbContext;

    public UpdateItemCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ItemResponse> Handle(UpdateItemCommand request, CancellationToken cancellationToken)
    {
        var item = await _dbContext.Items
            .FirstOrDefaultAsync(i => i.Id == request.ItemId && i.SellerId == request.SellerId, cancellationToken);

        if (item is null)
        {
            throw new InvalidOperationException("Item not found.");
        }

        var categoryExists = await _dbContext.Categories
            .AnyAsync(c => c.Id == request.CategoryId, cancellationToken);

        if (!categoryExists)
        {
            throw new InvalidOperationException("Category not found.");
        }

        if (!Enum.TryParse<ItemStatus>(request.Status, ignoreCase: true, out var status))
        {
            throw new InvalidOperationException("Invalid item status.");
        }

        item.UpdateDetails(request.Title, request.Description, request.Price, status);
        if (item.CategoryId != request.CategoryId)
        {
            item.ChangeCategory(request.CategoryId);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var response = await _dbContext.Items
            .AsNoTracking()
            .Where(i => i.Id == item.Id)
            .Include(i => i.Images)
            .Include(i => i.Category)
            .ProjectToType<ItemResponse>()
            .FirstAsync(cancellationToken);

        return response;
    }
}
