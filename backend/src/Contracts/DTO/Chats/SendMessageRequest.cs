using System.ComponentModel.DataAnnotations;

namespace Contracts.DTO.Chats;

public sealed class SendMessageRequest
{
    [Required]
    public Guid ToUserId { get; init; }

    public Guid? ItemId { get; init; }

    [Required]
    public string Body { get; init; } = string.Empty;

    public string? AttachmentUrl { get; init; }
}
