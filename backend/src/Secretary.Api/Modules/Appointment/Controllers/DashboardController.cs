using Secretary.Application.Dtos;
using Secretary.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NodaTime;
using Secretary.Domain.Enums;
using Secretary.Api.Auth;

namespace Secretary.Api.Controllers;

[ApiController]
[RequireModule(Module.Appointment)]
[Authorize(Roles = "Owner,Staff")]
[Route("api/dashboard")]
public sealed class DashboardController : ControllerBase
{
    private readonly DashboardService _dashboardService;

    public DashboardController(DashboardService dashboardService) => _dashboardService = dashboardService;

    [HttpGet("summary")]
    public async Task<ActionResult<DashboardSummaryResponse>> GetSummary(
        [FromQuery] Instant from, [FromQuery] Instant to, CancellationToken cancellationToken)
        => Ok(await _dashboardService.GetSummaryAsync(from, to, cancellationToken));
}
