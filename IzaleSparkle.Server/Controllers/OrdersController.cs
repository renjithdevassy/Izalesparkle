using MediatR;
using Microsoft.AspNetCore.Mvc;
using IzaleSparkle.Application.Orders.Commands;
using IzaleSparkle.Application.Admin.Commands;
using IzaleSparkle.Contracts.Requests;
using IzaleSparkle.Contracts.Responses;

namespace IzaleSparkle.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class OrdersController(IMediator mediator) : ControllerBase
{
    /// <summary>Place a new order.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<OrderResponse>), 201)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    [ProducesResponseType(typeof(ApiResponse<object>), 422)]
    public async Task<IActionResult> PlaceOrder([FromBody] PlaceOrderRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(new PlaceOrderCommand(req), ct);
        return CreatedAtAction(nameof(GetByNumber), new { orderNumber = result.OrderNumber },
            ApiResponse<OrderResponse>.Ok(result, "Order placed successfully."));
    }

    /// <summary>Get all orders for a customer by email.</summary>
    [HttpGet("my")]
    public async Task<IActionResult> GetMyOrders([FromQuery] string email, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(email))
            return BadRequest(ApiResponse<object>.Fail("Email required."));
        var orders = await mediator.Send(new GetOrdersByEmailQuery(email), ct);
        return Ok(ApiResponse<IEnumerable<OrderResponse>>.Ok(orders));
    }

    /// <summary>Cancel an order by order number — customer only, Pending status only.</summary>
    [HttpPost("{orderNumber}/cancel")]
    public async Task<IActionResult> CancelOrder(string orderNumber,
        [FromBody] CancelOrderRequest req, CancellationToken ct)
    {
        var order = await mediator.Send(new GetOrderByNumberQuery(orderNumber), ct);
        var result = await mediator.Send(
            new CancelOrderCommand(order.Id, req.Reason, IsCustomer: true), ct);
        return Ok(ApiResponse<bool>.Ok(true, "Order cancelled successfully."));
    }

    /// <summary>Validate a discount code and get the saving amount.</summary>
    [HttpPost("validate-discount")]
    public async Task<IActionResult> ValidateDiscount(
        [FromBody] ValidateDiscountRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(
            new ValidateDiscountCodeQuery(req.Code, req.Subtotal), ct);
        return Ok(ApiResponse<ValidateDiscountResponse>.Ok(result));
    }

    /// <summary>Get an order by order number (e.g. IS20240101120000123).</summary>
    [HttpGet("{orderNumber}")]
    [ProducesResponseType(typeof(ApiResponse<OrderResponse>), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetByNumber(string orderNumber, CancellationToken ct)
    {
        var result = await mediator.Send(new GetOrderByNumberQuery(orderNumber), ct);
        return Ok(ApiResponse<OrderResponse>.Ok(result));
    }
}
