using Contracts.DTO.Items;
using MediatR;

namespace Application.Items.Commands.CreateItem;

public sealed record CreateItemCommand(
    Guid SellerId,
    Guid CategoryId,
    string Title,
    string Description,
    decimal Price) : IRequest<ItemResponse>;
