using Contracts.DTO.Items;
using MediatR;

namespace Application.Items.Commands.UpdateItem;

public sealed record UpdateItemCommand(
    Guid ItemId,
    Guid SellerId,
    Guid CategoryId,
    string Title,
    string Description,
    decimal Price,
    string Status) : IRequest<ItemResponse>;
