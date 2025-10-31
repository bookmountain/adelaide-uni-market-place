using System.ComponentModel.DataAnnotations;

namespace Contracts.DTO.Orders;

public sealed class CreateOrderRequest
{
    [Required]
    public Guid ItemId { get; init; }

    [Required, MaxLength(256)]
    public string MeetingLocation { get; init; } = string.Empty;

    public DateTimeOffset? MeetingScheduledAt { get; init; }
}
