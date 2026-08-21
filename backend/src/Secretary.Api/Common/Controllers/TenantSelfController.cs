using Secretary.Application.Abstractions;
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
    private readonly TenantModuleService _moduleService;
    private readonly ICurrentTenantProvider _currentTenant;
    private readonly IValidator<UpdateOwnTenantRequest> _updateValidator;

    public TenantSelfController(
        TenantService tenantService,
        TenantModuleService moduleService,
        ICurrentTenantProvider currentTenant,
        IValidator<UpdateOwnTenantRequest> updateValidator)
    {
        _tenantService = tenantService;
        _moduleService = moduleService;
        _currentTenant = currentTenant;
        _updateValidator = updateValidator;
    }

    [HttpGet]
    [Authorize(Roles = "Owner,Staff")]
    public async Task<ActionResult<TenantResponse>> Get(CancellationToken cancellationToken)
        => Ok(await _tenantService.GetCurrentAsync(cancellationToken));

    /// <summary>Every module and whether this tenant holds it — the same shape the admin screen
    /// reads, for the tenant's own tenant.
    ///
    /// The full set, not only what was granted, so the module picker can show a business what
    /// else exists alongside what they bought. That is the difference between a picker and a
    /// menu: one of them tells you there is more.
    ///
    /// Read-only, and safe to be: it reveals the platform's module catalogue, which the public
    /// landing page already advertises in full.</summary>
    [HttpGet("modules")]
    [Authorize(Roles = "Owner,Staff")]
    public async Task<ActionResult<IReadOnlyList<TenantModuleResponse>>> GetModules(CancellationToken cancellationToken)
    {
        var tenantId = _currentTenant.TenantId
            ?? throw new InvalidOperationException("This endpoint requires a tenant-scoped caller.");

        return Ok(await _moduleService.ListAsync(tenantId, cancellationToken));
    }

    [HttpPut]
    [Authorize(Roles = "Owner")]
    public async Task<ActionResult<TenantResponse>> Update(
        UpdateOwnTenantRequest request, CancellationToken cancellationToken)
    {
        await _updateValidator.ValidateAndThrowAsync(request, cancellationToken);
        return Ok(await _tenantService.UpdateCurrentAsync(request, cancellationToken));
    }
}
