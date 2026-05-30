using System.ComponentModel.DataAnnotations;

namespace Contracts.DTO.Threads;

public sealed class CreateThreadCategoryRequest
{
    [Required, MaxLength(64)] public string Slug { get; init; } = string.Empty;
    [Required, MaxLength(128)] public string Name { get; init; } = string.Empty;
    [Required, MaxLength(512)] public string Description { get; init; } = string.Empty;
    [Required, MaxLength(64)] public string IconKey { get; init; } = string.Empty;
    public int SortOrder { get; init; }
}
