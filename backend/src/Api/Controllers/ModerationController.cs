using Application.Moderation.Commands.ResolveReport;
using Application.Moderation.Queries.GetReportQueue;
using Contracts.DTO.Moderation;
using Domain.Shared.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Api.Controllers;

[Authorize(Roles = "Admin")]
[ApiController]
[Route("api/threads/reports")]
public class ModerationController : ControllerBase
{
    private readonly ISender _sender;
    public ModerationController(ISender sender) => _sender = sender;

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ModerationReportResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Queue([FromQuery] ReportStatus status = ReportStatus.Open, CancellationToken ct = default)
        => Ok(await _sender.Send(new GetReportQueueQuery(status), ct));

    [HttpPost("{reportId:guid}/resolve")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Resolve(Guid reportId, [FromBody] ResolveReportRequest request, CancellationToken ct)
    {
        if (!TryGetUserId(out var adminId)) return Unauthorized();
        try
        {
            await _sender.Send(new ResolveReportCommand(reportId, adminId, request.Action), ct);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    private bool TryGetUserId(out Guid userId)
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(claim, out userId);
    }
}
