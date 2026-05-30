using Application.Threads.Commands.CreateThreadCategory;
using Application.Threads.Commands.UpdateThreadCategory;
using Application.Threads.Queries.GetThreadCategories;
using Contracts.DTO.Threads;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/threads/categories")]
public class ThreadCategoriesController : ControllerBase
{
    private readonly ISender _sender;
    public ThreadCategoriesController(ISender sender) => _sender = sender;

    [AllowAnonymous]
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ThreadCategoryResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(await _sender.Send(new GetThreadCategoriesQuery(), ct));

    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateThreadCategoryRequest request, CancellationToken ct)
    {
        try
        {
            var id = await _sender.Send(new CreateThreadCategoryCommand(
                request.Slug, request.Name, request.Description, request.IconKey, request.SortOrder), ct);
            return CreatedAtAction(nameof(List), new { id }, new { id });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [Authorize(Roles = "Admin")]
    [HttpPatch("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateThreadCategoryRequest request, CancellationToken ct)
    {
        try
        {
            await _sender.Send(new UpdateThreadCategoryCommand(
                id, request.Name, request.Description, request.IconKey, request.SortOrder, request.IsActive), ct);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
