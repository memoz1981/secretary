using Secretary.Api.Auth;
using Secretary.Application.Dtos;
using Secretary.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Secretary.Domain.Enums;
using NodaTime;

namespace Secretary.Api.Controllers;

/// <summary>How the ordering line is doing over a range.</summary>
[ApiController]
[RequireModule(Module.Order)]
[Authorize(Roles = "Owner,Staff")]
[Route("api/orders/dashboard")]
public sealed class OrderDashboardController : ControllerBase
{
    private readonly OrderDashboardService _dashboard;

    public OrderDashboardController(OrderDashboardService dashboard) => _dashboard = dashboard;

    [HttpGet]
    public async Task<ActionResult<OrderDashboardResponse>> Get(
        [FromQuery] Instant from, [FromQuery] Instant to, CancellationToken cancellationToken)
        => Ok(await _dashboard.GetAsync(from, to, cancellationToken));
}
