using System;
using System.Linq;
using System.Security.Claims;
using Application.Items.Commands.CreateItem;
using Application.Items.Commands.DeleteItem;
using Application.Items.Commands.DeleteItemImage;
using Application.Items.Commands.UpdateItem;
using Application.Items.Commands.UploadItemImage;
using Application.Items.Queries;
using Api.Models;
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

    [HttpGet("{itemId:guid}/images")]
    [ProducesResponseType(typeof(IReadOnlyCollection<ListingImageResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetItemImages(Guid itemId, CancellationToken cancellationToken)
    {
        var item = await _sender.Send(new GetItemByIdQuery(itemId), cancellationToken);
        if (item is null)
        {
            return NotFound();
        }

        var ordered = item.Images.OrderBy(image => image.SortOrder).ToList();
        return Ok(ordered);
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ItemResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CreateItemWithImages([FromForm] CreateItemWithImagesRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var sellerId))
        {
            return Unauthorized();
        }

        if (request.Images.Count == 0)
        {
            return BadRequest(new { error = "At least one image is required." });
        }

        try
        {
            var createRequest = new CreateItemRequest
            {
                CategoryId = request.CategoryId,
                Title = request.Title,
                Description = request.Description,
                Price = request.Price
            };

            var created = await CreateItemInternalAsync(sellerId, createRequest, cancellationToken);

            foreach (var file in request.Images.Where(f => f.Length > 0))
            {
                await using var stream = file.OpenReadStream();
                var uploadCommand = new UploadItemImageCommand(
                    created.Id,
                    sellerId,
                    stream,
                    file.FileName,
                    file.ContentType ?? "application/octet-stream");

                await _sender.Send(uploadCommand, cancellationToken);
            }

            var enriched = await _sender.Send(new GetItemByIdQuery(created.Id), cancellationToken) ?? created;
            return CreatedAtAction(nameof(GetItemImages), new { itemId = created.Id }, enriched);
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message.Equals("Image not found.", StringComparison.OrdinalIgnoreCase)
                ? NotFound(new { error = ex.Message })
                : BadRequest(new { error = ex.Message });
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

    [HttpPost("{itemId:guid}/images")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(IReadOnlyCollection<ListingImageResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UploadImages(
        Guid itemId,
        [FromForm(Name = "files")] List<IFormFile>? files,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var sellerId))
        {
            return Unauthorized();
        }

        if (files is null || files.Count == 0)
        {
            return BadRequest(new { error = "No files uploaded." });
        }

        var responses = new List<ListingImageResponse>(files.Count);

        foreach (var file in files)
        {
            if (file.Length == 0)
            {
                continue;
            }

            await using var stream = file.OpenReadStream();

            var command = new UploadItemImageCommand(
                itemId,
                sellerId,
                stream,
                file.FileName,
                file.ContentType ?? "application/octet-stream");

            try
            {
                var result = await _sender.Send(command, cancellationToken);
                responses.Add(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        if (responses.Count == 0)
        {
            return BadRequest(new { error = "No valid image files uploaded." });
        }

        return Ok(responses);
    }

    [HttpDelete("{itemId:guid}/images/{imageId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeleteImage(Guid itemId, Guid imageId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var sellerId))
        {
            return Unauthorized();
        }

        try
        {
            await _sender.Send(new DeleteItemImageCommand(itemId, imageId, sellerId), cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    private async Task<ItemResponse> CreateItemInternalAsync(Guid sellerId, CreateItemRequest request, CancellationToken cancellationToken)
    {
        var command = new CreateItemCommand(sellerId, request.CategoryId, request.Title, request.Description, request.Price);
        return await _sender.Send(command, cancellationToken);
    }

    private bool TryGetUserId(out Guid userId)
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(claim, out userId);
    }

}
