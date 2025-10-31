using System.Security.Claims;
using Api.Models;
using Application.Orders.Commands.CreateOrder;
using Application.Orders.Queries.GetMyOrders;
using Application.Orders.Queries.GetOrderById;
using Contracts.DTO.Orders;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public sealed class OrdersController : ControllerBase
{
    private readonly ISender _sender;

    public OrdersController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyCollection<OrderResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyOrders(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var buyerId))
        {
            return Unauthorized();
        }

        var orders = await _sender.Send(new GetMyOrdersQuery(buyerId), cancellationToken);
        return Ok(orders);
    }

    [HttpGet("{orderId:guid}")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOrderById(Guid orderId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var buyerId))
        {
            return Unauthorized();
        }

        var order = await _sender.Send(new GetOrderByIdQuery(orderId, buyerId), cancellationToken);
        return order is null ? NotFound() : Ok(order);
    }

    [HttpPost]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var buyerId))
        {
            return Unauthorized();
        }

        try
        {
            var command = new CreateOrderCommand(
                buyerId,
                request.ItemId,
                request.MeetingLocation,
                request.MeetingScheduledAt);

            var order = await _sender.Send(command, cancellationToken);
            return CreatedAtAction(nameof(GetOrderById), new { orderId = order.OrderId }, order);
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
