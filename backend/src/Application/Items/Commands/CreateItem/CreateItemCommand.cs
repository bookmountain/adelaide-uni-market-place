using Contracts.DTO.Items;
using Domain.Shared.Enums;
using MediatR;

namespace Application.Items.Commands.CreateItem;

public sealed record CreateItemCommand(
    Guid SellerId,
    Guid CategoryId,
    string Title,
    string Description,
    decimal Price,
    ItemCondition Condition,
    string MeetupLocation,
    string? Brand,
    bool IsNegotiable) : IRequest<ItemResponse>;
