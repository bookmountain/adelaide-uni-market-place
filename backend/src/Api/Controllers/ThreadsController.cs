using Api.Models;
using Application.Threads.Commands.CreateThreadComment;
using Application.Threads.Commands.CreateThreadPost;
using Application.Threads.Commands.DeleteThreadPost;
using Application.Threads.Commands.ToggleThreadLike;
using Application.Threads.Commands.UpdateThreadPost;
using Application.Threads.Queries.GetThreadComments;
using Application.Threads.Queries.GetThreadFeed;
using Application.Threads.Queries.GetThreadPost;
using Contracts.DTO.Threads;
using Domain.Shared.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Api.Controllers;

[Authorize]
[ApiController]
[Route("api/threads")]
public class ThreadsController : ControllerBase
{
    private readonly ISender _sender;
    public ThreadsController(ISender sender) => _sender = sender;

    [HttpGet("feed")]
    [ProducesResponseType(typeof(ThreadFeedResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Feed(
        [FromQuery] string? category, [FromQuery] string sort = "hot",
        [FromQuery] string? cursor = null, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => Ok(await _sender.Send(new GetThreadFeedQuery(category, sort, cursor, pageSize), ct));

    [HttpGet("posts/{postId:guid}")]
    [ProducesResponseType(typeof(ThreadPostDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPost(Guid postId, CancellationToken ct)
    {
        var post = await _sender.Send(new GetThreadPostQuery(postId), ct);
        return post is null ? NotFound() : Ok(post);
    }

    [HttpGet("posts/{postId:guid}/comments")]
    [ProducesResponseType(typeof(IReadOnlyList<ThreadCommentResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetComments(Guid postId, CancellationToken ct)
        => Ok(await _sender.Send(new GetThreadCommentsQuery(postId), ct));

    [HttpPost("posts")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreatePost([FromForm] CreateThreadPostRequest request, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();

        var images = new List<ThreadPostImageUpload>();
        foreach (var file in request.Images ?? new List<Microsoft.AspNetCore.Http.IFormFile>())
        {
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms, ct);
            images.Add(new ThreadPostImageUpload(ms.ToArray(), file.ContentType, file.FileName));
        }

        try
        {
            var id = await _sender.Send(new CreateThreadPostCommand(
                userId, request.CategoryId, request.Title, request.Body, request.IsAnonymous, images), ct);
            return CreatedAtAction(nameof(GetPost), new { postId = id }, new { id });
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPatch("posts/{postId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdatePost(Guid postId, [FromBody] UpdateThreadPostRequest request, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        try
        {
            await _sender.Send(new UpdateThreadPostCommand(postId, userId, request.Title, request.Body), ct);
            return NoContent();
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpDelete("posts/{postId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeletePost(Guid postId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var isAdmin = User.IsInRole("Admin");
        try
        {
            await _sender.Send(new DeleteThreadPostCommand(postId, userId, isAdmin), ct);
            return NoContent();
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("posts/{postId:guid}/like")]
    [ProducesResponseType(typeof(LikeResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> LikePost(Guid postId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        try { return Ok(await _sender.Send(new ToggleThreadLikeCommand(userId, ThreadLikeTarget.Post, postId), ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("posts/{postId:guid}/comments")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateComment(Guid postId, [FromBody] CreateThreadCommentRequest request, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        try
        {
            var id = await _sender.Send(new CreateThreadCommentCommand(
                postId, request.ParentCommentId, userId, request.IsAnonymous, request.Body), ct);
            return CreatedAtAction(nameof(GetComments), new { postId }, new { id });
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("comments/{commentId:guid}/like")]
    [ProducesResponseType(typeof(LikeResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> LikeComment(Guid commentId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        try { return Ok(await _sender.Send(new ToggleThreadLikeCommand(userId, ThreadLikeTarget.Comment, commentId), ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    private bool TryGetUserId(out Guid userId)
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(claim, out userId);
    }
}
