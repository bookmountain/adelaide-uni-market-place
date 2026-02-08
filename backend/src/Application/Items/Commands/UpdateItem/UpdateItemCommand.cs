using Contracts.DTO.Items;
using Domain.Shared.Enums;
using MediatR;

namespace Application.Items.Commands.UpdateItem;

public sealed record UpdateItemCommand(
    Guid ItemId,
    Guid SellerId,
    Guid CategoryId,
    string Title,
    string Description,
    decimal Price,
    string Status,
    ItemCondition Condition,
    string MeetupLocation,
    string? Brand,
    bool IsNegotiable) : IRequest<ItemResponse>;
