using Application.Common.Interfaces;
using Contracts.DTO.Items;
using Contracts.Events;
using Domain.Entities.Items;
using Domain.Shared.Enums;
using Mapster;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Items.Commands.CreateItem;

public sealed class CreateItemCommandHandler : IRequestHandler<CreateItemCommand, ItemResponse>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IEventPublisher _eventPublisher;

    public CreateItemCommandHandler(IApplicationDbContext dbContext, IEventPublisher eventPublisher)
    {
        _dbContext = dbContext;
        _eventPublisher = eventPublisher;
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
            condition: request.Condition,
            meetupLocation: request.MeetupLocation,
            brand: request.Brand,
            isNegotiable: request.IsNegotiable,
            createdAt: DateTimeOffset.UtcNow);

        _dbContext.Items.Add(entity);

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _eventPublisher.PublishAsync(new ItemCreatedEvent(
            entity.Id,
            entity.SellerId,
            entity.CategoryId,
            entity.Price,
            entity.CreatedAt),
            cancellationToken);

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
