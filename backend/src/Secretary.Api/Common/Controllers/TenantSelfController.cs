using Secretary.Application.Dtos;
using Secretary.Application.Services;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Secretary.Api.Controllers;

/// <summary>Tenant self-service (Admin page): an Owner reads and updates their own
/// business's details, without any platform-admin involvement. Staff can read (the page
/// shows the details) but only Owner can change them.</summary>
[ApiController]
[Route("api/tenant")]
public sealed class TenantSelfController : ControllerBase
{
    private readonly TenantService _tenantService;
    private readonly IValidator<UpdateTenantRequest> _updateValidator;

    public TenantSelfController(TenantService tenantService, IValidator<UpdateTenantRequest> updateValidator)
    {
        _tenantService = tenantService;
        _updateValidator = updateValidator;
    }

    [HttpGet]
    [Authorize(Roles = "Owner,Staff")]
    public async Task<ActionResult<TenantResponse>> Get(CancellationToken cancellationToken)
        => Ok(await _tenantService.GetCurrentAsync(cancellationToken));

    [HttpPut]
    [Authorize(Roles = "Owner")]
    public async Task<ActionResult<TenantResponse>> Update(UpdateTenantRequest request, CancellationToken cancellationToken)
    {
        await _updateValidator.ValidateAndThrowAsync(request, cancellationToken);
        return Ok(await _tenantService.UpdateCurrentAsync(request, cancellationToken));
    }
}
