using Application.Users.Commands.CreateReview;
using Application.Users.Queries.GetUserReviews;
using Contracts.DTO.Users;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly ISender _sender;

    public UsersController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet("{userId:guid}/reviews")]
    [ProducesResponseType(typeof(List<ReviewResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetReviews(Guid userId, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetUserReviewsQuery(userId), cancellationToken);
        return Ok(result);
    }

    [HttpPost("{userId:guid}/reviews")]
    [ProducesResponseType(typeof(ReviewResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CreateReview(Guid userId, [FromBody] CreateReviewRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var reviewerId))
        {
            return Unauthorized();
        }

        if (userId != request.TargetUserId)
        {
            return BadRequest(new { error = "Target user ID in URL does not match body." });
        }

        try
        {
            var command = new CreateReviewCommand(
                reviewerId,
                request.TargetUserId,
                request.OrderId,
                request.Rating,
                request.Comment);

            var result = await _sender.Send(command, cancellationToken);
            return Ok(result);
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
