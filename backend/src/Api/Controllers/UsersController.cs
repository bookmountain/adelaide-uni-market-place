using Application.Users.Commands.CreateReview;
using Application.Users.Commands.GetOrCreateAnonHandle;
using Application.Users.Commands.UpdateProfile;
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

    [HttpPatch("me")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateMe([FromBody] UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        await _sender.Send(new UpdateProfileCommand(userId, request.Bio, request.AppearInDrawPool), cancellationToken);
        return NoContent();
    }

    [HttpGet("me/anon-handle")]
    [ProducesResponseType(typeof(AnonHandleResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyAnonHandle(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var handle = await _sender.Send(new GetOrCreateAnonHandleCommand(userId), cancellationToken);
        return Ok(new AnonHandleResponse(handle));
    }

    private bool TryGetUserId(out Guid userId)
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(claim, out userId);
    }
}
