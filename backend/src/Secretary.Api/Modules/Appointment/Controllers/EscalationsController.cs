using Secretary.Api.Auth;
using Secretary.Api.Hubs;
using Secretary.Application.Dtos;
using Secretary.Application.Services;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Secretary.Domain.Enums;

namespace Secretary.Api.Controllers;

/// <summary>Backs the escalation ringing overlay (Flow D). Every state change is persisted
/// through EscalationService and then pushed to the tenant's connected Owner/Staff clients
/// over the EscalationHub SignalR channel, so the overlay updates live rather than needing
/// a poll.</summary>
[ApiController]
[RequireModule(Module.Appointment)]
[Route("api/escalations")]
public sealed class EscalationsController : ControllerBase
{
    private readonly EscalationService _escalationService;
    private readonly IValidator<RaiseEscalationRequest> _raiseValidator;
    private readonly IHubContext<EscalationHub> _hub;

    public EscalationsController(
        EscalationService escalationService, IValidator<RaiseEscalationRequest> raiseValidator, IHubContext<EscalationHub> hub)
    {
        _escalationService = escalationService;
        _raiseValidator = raiseValidator;
        _hub = hub;
    }

    [HttpGet("ringing")]
    [Authorize(Roles = "Owner,Staff")]
    public async Task<ActionResult<IReadOnlyList<EscalationResponse>>> GetRinging(CancellationToken cancellationToken)
        => Ok(await _escalationService.GetRingingAsync(cancellationToken));

    [HttpPost]
    [Authorize(Roles = "Agent")]
    public async Task<ActionResult<EscalationResponse>> Raise(RaiseEscalationRequest request, CancellationToken cancellationToken)
    {
        await _raiseValidator.ValidateAndThrowAsync(request, cancellationToken);
        var escalation = await _escalationService.RaiseAsync(request, cancellationToken);
        await BroadcastAsync(escalation, "escalationRinging", cancellationToken);
        return Ok(escalation);
    }

    [HttpPost("{id:int}/accept")]
    [Authorize(Roles = "Owner,Staff")]
    public async Task<ActionResult<EscalationResponse>> Accept(int id, CancellationToken cancellationToken)
    {
        var acceptedBy = int.Parse(User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value
            ?? throw new InvalidOperationException("Missing account id claim."));

        var escalation = await _escalationService.AcceptAsync(id, acceptedBy, cancellationToken);
        await BroadcastAsync(escalation, "escalationConnected", cancellationToken);
        return Ok(escalation);
    }

    [HttpPost("{id:int}/end")]
    [Authorize(Roles = "Owner,Staff")]
    public async Task<IActionResult> End(int id, CancellationToken cancellationToken)
    {
        var escalation = await _escalationService.EndConnectedCallAsync(id, cancellationToken);
        await BroadcastAsync(escalation, "escalationEnded", cancellationToken);
        return NoContent();
    }

    private async Task BroadcastAsync(EscalationResponse escalation, string eventName, CancellationToken cancellationToken)
    {
        var tenantId = User.FindFirst(AppClaimTypes.TenantId)?.Value;
        if (tenantId is not null)
        {
            await _hub.Clients.Group(EscalationHub.TenantGroup(tenantId)).SendAsync(eventName, escalation, cancellationToken);
        }
    }
}
