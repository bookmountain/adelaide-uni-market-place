namespace Contracts.DTO.Items;

public sealed record ListItemsResponse(IReadOnlyCollection<ItemResponse> Items);
