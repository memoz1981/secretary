using Secretary.Api.Auth;
using Secretary.Application.Dtos;
using Secretary.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Secretary.Domain.Enums;

namespace Secretary.Api.Controllers;

/// <summary>What the phone line took: orders, the customers it built up, and the delivery
/// promise those orders are made against. Read-only apart from the promise — orders arrive by
/// phone, they are not typed in.</summary>
[ApiController]
[RequireModule(Module.Order)]
[Authorize(Roles = "Owner,Staff")]
[Route("api/orders")]
public sealed class OrdersController : ControllerBase
{
    private readonly OrderService _orders;

    public OrdersController(OrderService orders) => _orders = orders;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<OrderResponse>>> List(CancellationToken cancellationToken)
        => Ok(await _orders.ListAsync(cancellationToken));

    /// <summary>Delivered or cancelled — the only writes on this page, and both are facts only a
    /// person has. Owner-only, like every other mutation.</summary>
    [HttpPost("{id:int}/status")]
    [Authorize(Roles = "Owner")]
    public async Task<ActionResult<OrderResponse>> SetStatus(
        [FromRoute] int id, SetOrderStatusRequest request, CancellationToken cancellationToken)
    {
        if (request.Status is not (OrderStatus.Delivered or OrderStatus.Cancelled))
        {
            return BadRequest("An order can only be marked delivered or cancelled.");
        }

        try
        {
            return Ok(await _orders.SetStatusAsync(id, request.Status, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            // Already delivered or already cancelled — a second click, or two people on the same
            // list at once.
            return Conflict(ex.Message);
        }
    }

    [HttpGet("customers")]
    public async Task<ActionResult<IReadOnlyList<CustomerResponse>>> Customers(CancellationToken cancellationToken)
        => Ok(await _orders.ListCustomersAsync(cancellationToken));

    [HttpGet("settings")]
    public async Task<ActionResult<OrderSettingsResponse>> Settings(CancellationToken cancellationToken)
        => Ok(await _orders.GetSettingsAsync(cancellationToken));

    [HttpPut("settings")]
    [Authorize(Roles = "Owner")]
    public async Task<ActionResult<OrderSettingsResponse>> UpdateSettings(
        UpdateOrderSettingsRequest request, CancellationToken cancellationToken)
    {
        if (request.LeadWorkingDays is < 0 or > 14)
        {
            return BadRequest("Delivery lead time must be between 0 and 14 working days.");
        }

        return Ok(await _orders.UpdateSettingsAsync(request, cancellationToken));
    }
}
