using Secretary.Api.Auth;
using Secretary.Application.Dtos;
using Secretary.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Secretary.Domain.Enums;
using NodaTime;

namespace Secretary.Api.Controllers;

/// <summary>The order line's call log. Read-only: every row was written by a call ending, and a
/// form that could add one would be a form that disagreed with what happened.</summary>
[ApiController]
[RequireModule(Module.Order)]
[Authorize(Roles = "Owner,Staff")]
[Route("api/orders/calls")]
public sealed class OrderCallsController : ControllerBase
{
    private readonly OrderCallService _calls;

    public OrderCallsController(OrderCallService calls) => _calls = calls;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<OrderCallResponse>>> Search(
        [FromQuery] Instant? from, [FromQuery] Instant? to, [FromQuery] CallOutcome? outcome,
        CancellationToken cancellationToken)
        => Ok(await _calls.SearchAsync(from, to, outcome, cancellationToken));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<OrderCallDetailResponse>> Detail(
        [FromRoute] int id, CancellationToken cancellationToken)
        => Ok(await _calls.GetDetailAsync(id, cancellationToken));
}
