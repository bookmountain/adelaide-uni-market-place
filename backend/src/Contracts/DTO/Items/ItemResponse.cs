using Domain.Shared.Enums;

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
    ItemCondition Condition,
    string MeetupLocation,
    string? Brand,
    bool IsNegotiable,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    IReadOnlyCollection<ListingImageResponse> Images);

public sealed record ListingImageResponse(Guid Id, string Url, int SortOrder);
