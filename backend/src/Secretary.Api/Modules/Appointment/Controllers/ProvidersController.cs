using Secretary.Application.Dtos;
using Secretary.Application.Services;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Secretary.Domain.Enums;
using Secretary.Api.Auth;

namespace Secretary.Api.Controllers;

/// <summary>Providers page — list/add/edit/remove providers plus the provider×service
/// checkbox matrix. Reads are open to Staff and the Agent (the agent needs providers
/// filtered by offering); mutations are Owner-only.</summary>
[ApiController]
[RequireModule(Module.Appointment)]
[Route("api/providers")]
public sealed class ProvidersController : ControllerBase
{
    private readonly ProviderService _providerService;
    private readonly IValidator<CreateProviderRequest> _createValidator;
    private readonly IValidator<UpdateProviderRequest> _updateValidator;

    public ProvidersController(
        ProviderService providerService, IValidator<CreateProviderRequest> createValidator, IValidator<UpdateProviderRequest> updateValidator)
    {
        _providerService = providerService;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    [HttpGet]
    [Authorize(Roles = "Owner,Staff,Agent")]
    public async Task<ActionResult<IReadOnlyList<ProviderResponse>>> List(
        [FromQuery] int? serviceOfferingId, CancellationToken cancellationToken)
        => Ok(serviceOfferingId is int offeringId
            ? await _providerService.ListForOfferingAsync(offeringId, cancellationToken)
            : await _providerService.ListAsync(cancellationToken));

    [HttpGet("service-matrix")]
    [Authorize(Roles = "Owner,Staff")]
    public async Task<ActionResult<ProviderServiceMatrixResponse>> GetServiceMatrix(CancellationToken cancellationToken)
        => Ok(await _providerService.GetServiceMatrixAsync(cancellationToken));

    [HttpPut("{id:int}/service-assignments")]
    [Authorize(Roles = "Owner,Staff")]
    public async Task<IActionResult> SetServiceAssignment(
        [FromRoute] int id, SetProviderServiceAssignmentRequest request, CancellationToken cancellationToken)
    {
        await _providerService.SetServiceAssignmentAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpPost]
    [Authorize(Roles = "Owner")]
    public async Task<ActionResult<ProviderResponse>> Create(CreateProviderRequest request, CancellationToken cancellationToken)
    {
        await _createValidator.ValidateAndThrowAsync(request, cancellationToken);
        return Ok(await _providerService.CreateAsync(request, cancellationToken));
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Owner")]
    public async Task<ActionResult<ProviderResponse>> Update([FromRoute] int id, UpdateProviderRequest request, CancellationToken cancellationToken)
    {
        await _updateValidator.ValidateAndThrowAsync(request, cancellationToken);
        return Ok(await _providerService.UpdateAsync(id, request, cancellationToken));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Owner")]
    public async Task<IActionResult> Remove(int id, CancellationToken cancellationToken)
    {
        await _providerService.RemoveAsync(id, cancellationToken);
        return NoContent();
    }
}
