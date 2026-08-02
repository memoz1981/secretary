using Secretary.Application.Dtos;
using Secretary.Application.Services;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Secretary.Api.Controllers;

/// <summary>Account self-service half of the Admin page — Owner only. Accounts exist for
/// audit trail; removal soft-deactivates and password reset is done here by the Owner.</summary>
[ApiController]
[Authorize(Roles = "Owner")]
[Route("api/staff-accounts")]
public sealed class StaffAccountsController : ControllerBase
{
    private readonly AccountService _accountService;
    private readonly IValidator<AddStaffRequest> _addValidator;
    private readonly IValidator<UpdateAccountRequest> _updateValidator;
    private readonly IValidator<ResetAccountPasswordRequest> _resetPasswordValidator;

    public StaffAccountsController(
        AccountService accountService,
        IValidator<AddStaffRequest> addValidator,
        IValidator<UpdateAccountRequest> updateValidator,
        IValidator<ResetAccountPasswordRequest> resetPasswordValidator)
    {
        _accountService = accountService;
        _addValidator = addValidator;
        _updateValidator = updateValidator;
        _resetPasswordValidator = resetPasswordValidator;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AccountResponse>>> List(CancellationToken cancellationToken)
        => Ok(await _accountService.ListAsync(cancellationToken));

    [HttpPost]
    public async Task<ActionResult<AccountResponse>> Add(AddStaffRequest request, CancellationToken cancellationToken)
    {
        await _addValidator.ValidateAndThrowAsync(request, cancellationToken);
        return Ok(await _accountService.AddStaffAsync(request, cancellationToken));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<AccountResponse>> Update([FromRoute] int id, UpdateAccountRequest request, CancellationToken cancellationToken)
    {
        await _updateValidator.ValidateAndThrowAsync(request, cancellationToken);
        return Ok(await _accountService.UpdateAsync(id, request, cancellationToken));
    }

    [HttpPost("{id:int}/reset-password")]
    public async Task<IActionResult> ResetPassword([FromRoute] int id, ResetAccountPasswordRequest request, CancellationToken cancellationToken)
    {
        await _resetPasswordValidator.ValidateAndThrowAsync(request, cancellationToken);
        await _accountService.ResetPasswordAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Remove(int id, CancellationToken cancellationToken)
    {
        await _accountService.RemoveAsync(id, cancellationToken);
        return NoContent();
    }
}
