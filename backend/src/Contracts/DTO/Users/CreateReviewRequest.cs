using System.ComponentModel.DataAnnotations;

namespace Contracts.DTO.Users;

public sealed class CreateReviewRequest
{
    [Required]
    public Guid TargetUserId { get; init; }

    public Guid? OrderId { get; init; }

    [Required]
    [Range(1, 5)]
    public int Rating { get; init; }

    [Required]
    [MaxLength(1000)]
    public string Comment { get; init; } = string.Empty;
}
