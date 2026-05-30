using System.ComponentModel.DataAnnotations;

namespace Contracts.DTO.Threads;

public sealed class CreateThreadCommentRequest
{
    public Guid? ParentCommentId { get; init; }
    public bool IsAnonymous { get; init; }
    [Required] public string Body { get; init; } = string.Empty;
}
