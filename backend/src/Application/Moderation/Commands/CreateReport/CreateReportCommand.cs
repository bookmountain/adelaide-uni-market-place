using Domain.Shared.Enums;
using MediatR;

namespace Application.Moderation.Commands.CreateReport;

public sealed record CreateReportCommand(Guid ReporterUserId, ReportTargetType TargetType, Guid TargetId, ReportReason Reason, string? Notes)
    : IRequest<Guid>;
