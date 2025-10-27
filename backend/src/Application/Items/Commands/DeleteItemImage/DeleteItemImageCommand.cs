using MediatR;

namespace Application.Items.Commands.DeleteItemImage;

public sealed record DeleteItemImageCommand(Guid ItemId, Guid ImageId, Guid SellerId) : IRequest;

