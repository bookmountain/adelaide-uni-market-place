using System.ComponentModel.DataAnnotations;
using Domain.Shared.Enums;

namespace Contracts.DTO.Moderation;

public sealed class CreateReportRequest
{
    [Required] public ReportReason Reason { get; init; }
    [MaxLength(1000)] public string? Notes { get; init; }
}
