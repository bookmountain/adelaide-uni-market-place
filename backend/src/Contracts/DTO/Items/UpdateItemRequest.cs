using System.ComponentModel.DataAnnotations;
using Domain.Shared.Enums;

namespace Contracts.DTO.Items;

public sealed class UpdateItemRequest
{
    [Required]
    public Guid CategoryId { get; init; }

    [Required, MaxLength(160)]
    public string Title { get; init; } = string.Empty;

    [Required]
    public string Description { get; init; } = string.Empty;

    [Range(0.01, double.MaxValue)]
    public decimal Price { get; init; }

    [Required]
    public string Status { get; init; } = string.Empty;

    [Required]
    public ItemCondition Condition { get; init; }

    [Required, MaxLength(256)]
    public string MeetupLocation { get; init; } = string.Empty;

    [MaxLength(128)]
    public string? Brand { get; init; }

    public bool IsNegotiable { get; init; }
}
