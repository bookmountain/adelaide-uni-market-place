using System.Security.Claims;
using Application.Items.Commands.CreateItem;
using Application.Items.Commands.DeleteItem;
using Application.Items.Commands.UpdateItem;
using Application.Items.Queries;
using Contracts.DTO.Items;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ItemsController : ControllerBase
{
    private readonly ISender _sender;

    public ItemsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ListItemsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetItems(CancellationToken cancellationToken)
    {
        var response = await _sender.Send(new GetItemsQuery(), cancellationToken);
        return Ok(response);
    }

    [HttpGet("{itemId:guid}")]
    [ProducesResponseType(typeof(ItemResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetItemById(Guid itemId, CancellationToken cancellationToken)
    {
        var item = await _sender.Send(new GetItemByIdQuery(itemId), cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    [ProducesResponseType(typeof(ItemResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CreateItem([FromBody] CreateItemRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var sellerId))
        {
            return Unauthorized();
        }

        try
        {
            var command = new CreateItemCommand(sellerId, request.CategoryId, request.Title, request.Description, request.Price);
            var created = await _sender.Send(command, cancellationToken);
            return CreatedAtAction(nameof(GetItemById), new { itemId = created.Id }, created);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{itemId:guid}")]
    [ProducesResponseType(typeof(ItemResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateItem(Guid itemId, [FromBody] UpdateItemRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var sellerId))
        {
            return Unauthorized();
        }

        try
        {
            var command = new UpdateItemCommand(itemId, sellerId, request.CategoryId, request.Title, request.Description, request.Price, request.Status);
            var updated = await _sender.Send(command, cancellationToken);
            return Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            var status = ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase)
                ? StatusCodes.Status404NotFound
                : StatusCodes.Status400BadRequest;

            return status == StatusCodes.Status404NotFound
                ? NotFound(new { error = ex.Message })
                : BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("{itemId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteItem(Guid itemId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var sellerId))
        {
            return Unauthorized();
        }

        try
        {
            await _sender.Send(new DeleteItemCommand(itemId, sellerId), cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    private bool TryGetUserId(out Guid userId)
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(claim, out userId);
    }
}
