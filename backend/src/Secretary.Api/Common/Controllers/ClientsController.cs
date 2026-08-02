using Secretary.Application.Dtos;
using Secretary.Application.Services;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Secretary.Api.Controllers;

/// <summary>Clients page (list/add/edit/remove + blacklist) for Owner/Staff, plus the
/// phone-number lookups the AI agent uses mid-call.</summary>
[ApiController]
[Authorize(Roles = "Owner,Staff,Agent")]
[Route("api/clients")]
public sealed class ClientsController : ControllerBase
{
    private readonly ClientService _clientService;
    private readonly IValidator<CreateClientRequest> _createValidator;
    private readonly IValidator<UpdateClientRequest> _updateValidator;

    public ClientsController(
        ClientService clientService,
        IValidator<CreateClientRequest> createValidator,
        IValidator<UpdateClientRequest> updateValidator)
    {
        _clientService = clientService;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    [HttpGet]
    [Authorize(Roles = "Owner,Staff")]
    public async Task<ActionResult<IReadOnlyList<ClientResponse>>> List(CancellationToken cancellationToken)
        => Ok(await _clientService.ListAsync(cancellationToken));

    [HttpGet("by-phone/{phoneNumber}")]
    public async Task<ActionResult<ClientResponse>> GetByPhone(string phoneNumber, CancellationToken cancellationToken)
    {
        var client = await _clientService.GetByPhoneNumberAsync(phoneNumber, cancellationToken);
        return client is null ? NotFound() : Ok(client);
    }

    [HttpGet("{id:int}/upcoming-appointments")]
    public async Task<ActionResult<IReadOnlyList<AppointmentResponse>>> GetUpcomingAppointments(int id, CancellationToken cancellationToken)
        => Ok(await _clientService.GetUpcomingAppointmentsAsync(id, cancellationToken));

    [HttpPost]
    [Authorize(Roles = "Owner,Staff")]
    public async Task<ActionResult<ClientResponse>> Create(CreateClientRequest request, CancellationToken cancellationToken)
    {
        await _createValidator.ValidateAndThrowAsync(request, cancellationToken);
        return Ok(await _clientService.CreateAsync(request, cancellationToken));
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Owner,Staff")]
    public async Task<ActionResult<ClientResponse>> Update([FromRoute] int id, UpdateClientRequest request, CancellationToken cancellationToken)
    {
        await _updateValidator.ValidateAndThrowAsync(request, cancellationToken);
        return Ok(await _clientService.UpdateAsync(id, request, cancellationToken));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Owner,Staff")]
    public async Task<IActionResult> Remove(int id, CancellationToken cancellationToken)
    {
        await _clientService.RemoveAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:int}/blacklist")]
    [Authorize(Roles = "Owner,Staff")]
    public async Task<ActionResult<ClientResponse>> BlackList([FromRoute] int id, BlackListClientRequest request, CancellationToken cancellationToken)
        => Ok(await _clientService.BlackListAsync(id, request, cancellationToken));

    [HttpPost("{id:int}/undo-blacklist")]
    [Authorize(Roles = "Owner,Staff")]
    public async Task<ActionResult<ClientResponse>> UndoBlackList(int id, CancellationToken cancellationToken)
        => Ok(await _clientService.UndoBlackListAsync(id, cancellationToken));
}
