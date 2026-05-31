using System.ComponentModel.DataAnnotations;

namespace Contracts.DTO.Moderation;

public sealed class ResolveReportRequest
{
    /// <summary>One of: dismiss, remove-content, warn-user.</summary>
    [Required] public string Action { get; init; } = string.Empty;
}
