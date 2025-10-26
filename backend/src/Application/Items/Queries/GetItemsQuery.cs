using Contracts.DTO.Items;
using MediatR;

namespace Application.Items.Queries;

public sealed record GetItemsQuery() : IRequest<ListItemsResponse>;
