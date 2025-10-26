using System.ComponentModel.DataAnnotations;

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
}
