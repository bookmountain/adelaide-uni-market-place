using Application.Common.Interfaces;
using Contracts.DTO.Items;
using Domain.Entities.Items;
using Mapster;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Items.Commands.UploadItemImage;

public sealed class UploadItemImageCommandHandler : IRequestHandler<UploadItemImageCommand, ListingImageResponse>
{
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/jpg",
        "image/png",
        "image/gif",
        "image/webp"
    };

    private readonly IApplicationDbContext _dbContext;
    private readonly IObjectStorageService _storageService;

    public UploadItemImageCommandHandler(IApplicationDbContext dbContext, IObjectStorageService storageService)
    {
        _dbContext = dbContext;
        _storageService = storageService;
    }

    public async Task<ListingImageResponse> Handle(UploadItemImageCommand request, CancellationToken cancellationToken)
    {
        if (!AllowedContentTypes.Contains(request.ContentType))
        {
            throw new InvalidOperationException("Unsupported image format. Please upload JPEG, PNG, GIF, or WebP files.");
        }

        var item = await _dbContext.Items
            .Include(i => i.Images)
            .FirstOrDefaultAsync(i => i.Id == request.ItemId, cancellationToken);

        if (item is null)
        {
            throw new InvalidOperationException("Item not found.");
        }

        if (item.SellerId != request.SellerId)
        {
            throw new InvalidOperationException("You are not allowed to modify this item.");
        }

        if (request.Content.CanSeek)
        {
            request.Content.Position = 0;
        }

        var prefix = $"items/{item.Id}";

        var uploadResult = await _storageService.UploadAsync(prefix, request.Content, request.FileName, request.ContentType, cancellationToken);

        var nextOrder = item.Images.Any() ? item.Images.Max(i => i.SortOrder) + 1 : 1;
        var image = new ListingImage(Guid.NewGuid(), item.Id, uploadResult.Url, nextOrder, uploadResult.Key);

        item.AddImage(image);

        await _dbContext.SaveChangesAsync(cancellationToken);

        var response = image.Adapt<ListingImageResponse>();
        return response;
    }
}
