using MediatR;

namespace Application.Moderation.Commands.ResolveReport;

public sealed record ResolveReportCommand(Guid ReportId, Guid AdminUserId, string Action) : IRequest;
