using Contracts.DTO.Items;
using MediatR;

namespace Application.Items.Commands.UploadItemImage;

public sealed record UploadItemImageCommand(
    Guid ItemId,
    Guid SellerId,
    Stream Content,
    string FileName,
    string ContentType) : IRequest<ListingImageResponse>;

