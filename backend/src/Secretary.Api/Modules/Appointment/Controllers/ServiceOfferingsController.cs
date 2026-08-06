using Secretary.Application.Dtos;
using Secretary.Application.Services;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Secretary.Domain.Enums;
using Secretary.Api.Auth;

namespace Secretary.Api.Controllers;

/// <summary>Services page — Owner read/write, Staff read-only (page-inventory.md /
/// component-specs.md's cross-cutting note: same page, controls just absent for Staff).</summary>
[ApiController]
[RequireModule(Module.Appointment)]
[Authorize(Roles = "Owner,Staff")]
[Route("api/service-offerings")]
public sealed class ServiceOfferingsController : ControllerBase
{
    private readonly ServiceOfferingService _serviceOfferingService;
    private readonly IValidator<CreateServiceOfferingRequest> _createValidator;
    private readonly IValidator<UpdateServiceOfferingRequest> _updateValidator;

    public ServiceOfferingsController(
        ServiceOfferingService serviceOfferingService,
        IValidator<CreateServiceOfferingRequest> createValidator,
        IValidator<UpdateServiceOfferingRequest> updateValidator)
    {
        _serviceOfferingService = serviceOfferingService;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ServiceOfferingResponse>>> List(CancellationToken cancellationToken)
        => Ok(await _serviceOfferingService.ListAsync(cancellationToken));

    [HttpPost]
    [Authorize(Roles = "Owner")]
    public async Task<ActionResult<ServiceOfferingResponse>> Create(CreateServiceOfferingRequest request, CancellationToken cancellationToken)
    {
        await _createValidator.ValidateAndThrowAsync(request, cancellationToken);
        return Ok(await _serviceOfferingService.CreateAsync(request, cancellationToken));
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Owner")]
    public async Task<ActionResult<ServiceOfferingResponse>> Update([FromRoute] int id, UpdateServiceOfferingRequest request, CancellationToken cancellationToken)
    {
        await _updateValidator.ValidateAndThrowAsync(request, cancellationToken);
        return Ok(await _serviceOfferingService.UpdateAsync(id, request, cancellationToken));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Owner")]
    public async Task<IActionResult> Remove(int id, CancellationToken cancellationToken)
    {
        await _serviceOfferingService.RemoveAsync(id, cancellationToken);
        return NoContent();
    }
}
