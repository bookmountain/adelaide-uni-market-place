using MediatR;

namespace Application.Items.Commands.DeleteItem;

public sealed record DeleteItemCommand(Guid ItemId, Guid SellerId) : IRequest;
