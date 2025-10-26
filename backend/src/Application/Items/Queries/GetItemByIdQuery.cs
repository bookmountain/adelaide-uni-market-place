using Contracts.DTO.Items;
using MediatR;

namespace Application.Items.Queries;

public sealed record GetItemByIdQuery(Guid ItemId) : IRequest<ItemResponse?>;
