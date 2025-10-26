using Application.Common.Interfaces;
using Contracts.DTO.Items;
using Domain.Entities.Items;
using Domain.Shared.Enums;
using Mapster;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Items.Commands.CreateItem;

public sealed class CreateItemCommandHandler : IRequestHandler<CreateItemCommand, ItemResponse>
{
    private readonly IApplicationDbContext _dbContext;

    public CreateItemCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ItemResponse> Handle(CreateItemCommand request, CancellationToken cancellationToken)
    {
        var categoryExists = await _dbContext.Categories
            .AnyAsync(c => c.Id == request.CategoryId, cancellationToken);

        if (!categoryExists)
        {
            throw new InvalidOperationException("Category not found.");
        }

        var entity = new Item(
            id: Guid.NewGuid(),
            sellerId: request.SellerId,
            categoryId: request.CategoryId,
            title: request.Title,
            description: request.Description,
            price: request.Price,
            status: ItemStatus.Active,
            createdAt: DateTimeOffset.UtcNow);

        _dbContext.Items.Add(entity);

        await _dbContext.SaveChangesAsync(cancellationToken);

        var result = await _dbContext.Items
            .AsNoTracking()
            .Where(i => i.Id == entity.Id)
            .Include(i => i.Images)
            .Include(i => i.Category)
            .ProjectToType<ItemResponse>()
            .FirstAsync(cancellationToken);

        return result;
    }
}
