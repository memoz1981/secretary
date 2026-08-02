using Secretary.Application.Dtos;
using Secretary.Application.Services;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Secretary.Api.Controllers;

[ApiController]
[Authorize(Roles = "PlatformAdmin")]
[Route("api/tenants")]
public sealed class TenantsController : ControllerBase
{
    private readonly TenantService _tenantService;
    private readonly IValidator<CreateTenantRequest> _createValidator;
    private readonly IValidator<UpdateTenantRequest> _updateValidator;

    public TenantsController(
        TenantService tenantService, IValidator<CreateTenantRequest> createValidator, IValidator<UpdateTenantRequest> updateValidator)
    {
        _tenantService = tenantService;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TenantResponse>>> List([FromQuery] string? search, CancellationToken cancellationToken)
        => Ok(await _tenantService.ListAsync(search, cancellationToken));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<TenantResponse>> Get(int id, CancellationToken cancellationToken)
        => Ok(await _tenantService.GetAsync(id, cancellationToken));

    [HttpGet("{id:int}/owner-accounts")]
    public async Task<ActionResult<IReadOnlyList<AccountResponse>>> GetOwnerAccounts(int id, CancellationToken cancellationToken)
        => Ok(await _tenantService.GetOwnerAccountsAsync(id, cancellationToken));

    [HttpPost]
    public async Task<ActionResult<CreateTenantResult>> Create(CreateTenantRequest request, CancellationToken cancellationToken)
    {
        await _createValidator.ValidateAndThrowAsync(request, cancellationToken);
        var result = await _tenantService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = result.Tenant.Id }, result);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<TenantResponse>> Update([FromRoute] int id, UpdateTenantRequest request, CancellationToken cancellationToken)
    {
        await _updateValidator.ValidateAndThrowAsync(request, cancellationToken);
        return Ok(await _tenantService.UpdateAsync(id, request, cancellationToken));
    }

    [HttpPost("{id:int}/deactivate")]
    public async Task<IActionResult> Deactivate(int id, CancellationToken cancellationToken)
    {
        await _tenantService.DeactivateAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:int}/reactivate")]
    public async Task<IActionResult> Reactivate(int id, CancellationToken cancellationToken)
    {
        await _tenantService.ReactivateAsync(id, cancellationToken);
        return NoContent();
    }
}
