using System.Linq;
using Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Items.Commands.DeleteItemImage;

public sealed class DeleteItemImageCommandHandler : IRequestHandler<DeleteItemImageCommand>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IObjectStorageService _storageService;

    public DeleteItemImageCommandHandler(IApplicationDbContext dbContext, IObjectStorageService storageService)
    {
        _dbContext = dbContext;
        _storageService = storageService;
    }

    public async Task Handle(DeleteItemImageCommand request, CancellationToken cancellationToken)
    {
        var image = await _dbContext.ListingImages
            .Include(li => li.Item)
            .FirstOrDefaultAsync(li => li.Id == request.ImageId && li.ItemId == request.ItemId, cancellationToken);

        if (image is null)
        {
            throw new InvalidOperationException("Image not found.");
        }

        if (image.Item?.SellerId != request.SellerId)
        {
            throw new InvalidOperationException("You are not allowed to modify this item.");
        }

        var remainingImages = await _dbContext.ListingImages
            .Where(li => li.ItemId == request.ItemId && li.Id != request.ImageId)
            .OrderBy(li => li.SortOrder)
            .ToListAsync(cancellationToken);

        _dbContext.ListingImages.Remove(image);

        for (var index = 0; index < remainingImages.Count; index++)
        {
            remainingImages[index].UpdateSortOrder(index + 1);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(image.StorageKey))
        {
            await _storageService.DeleteAsync(image.StorageKey, cancellationToken);
        }
    }
}
