using System.ComponentModel.DataAnnotations;

namespace Contracts.DTO.Threads;

public sealed class UpdateThreadPostRequest
{
    [Required, MaxLength(200)] public string Title { get; init; } = string.Empty;
    [Required] public string Body { get; init; } = string.Empty;
}
