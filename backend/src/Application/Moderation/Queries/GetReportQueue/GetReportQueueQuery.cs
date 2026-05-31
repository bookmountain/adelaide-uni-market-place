using Contracts.DTO.Moderation;
using Domain.Shared.Enums;
using MediatR;

namespace Application.Moderation.Queries.GetReportQueue;

public sealed record GetReportQueueQuery(ReportStatus Status) : IRequest<IReadOnlyList<ModerationReportResponse>>;
