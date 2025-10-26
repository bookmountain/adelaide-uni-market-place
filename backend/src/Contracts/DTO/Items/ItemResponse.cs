namespace Contracts.DTO.Items;

public sealed record ItemResponse(
    Guid Id,
    Guid CategoryId,
    string CategoryName,
    Guid SellerId,
    string Title,
    string Description,
    decimal Price,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    IReadOnlyCollection<ListingImageResponse> Images);

public sealed record ListingImageResponse(Guid Id, string Url, int SortOrder);
