using System.Security.Claims;
using Application.Chats.Queries.GetChatHistory;
using Contracts.DTO.Chats;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public sealed class ChatsController : ControllerBase
{
    private readonly ISender _sender;

    public ChatsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet("{otherUserId:guid}/messages")]
    [ProducesResponseType(typeof(List<ChatMessageDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetChatHistory(Guid otherUserId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var messages = await _sender.Send(new GetChatHistoryQuery(currentUserId, otherUserId), cancellationToken);
        return Ok(messages);
    }

    private bool TryGetUserId(out Guid userId)
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(claim, out userId);
    }
}