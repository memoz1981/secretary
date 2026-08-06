using Secretary.Application.Dtos;
using Secretary.Application.Services;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Secretary.Domain.Enums;
using Secretary.Api.Auth;

namespace Secretary.Api.Controllers;

/// <summary>Call Log + Call Detail (Owner/Staff read) and the Agent's end-of-call log write.
/// There is deliberately no delete endpoint — calls are a permanent record.</summary>
[ApiController]
[RequireModule(Module.Appointment)]
[Route("api/calls")]
public sealed class CallsController : ControllerBase
{
    private readonly CallService _callService;
    private readonly IValidator<LogCallRequest> _logValidator;

    public CallsController(CallService callService, IValidator<LogCallRequest> logValidator)
    {
        _callService = callService;
        _logValidator = logValidator;
    }

    [HttpGet]
    [Authorize(Roles = "Owner,Staff")]
    public async Task<ActionResult<IReadOnlyList<CallResponse>>> Search([FromQuery] CallSearchRequest request, CancellationToken cancellationToken)
        => Ok(await _callService.SearchAsync(request, cancellationToken));

    [HttpGet("{id:int}")]
    [Authorize(Roles = "Owner,Staff")]
    public async Task<ActionResult<CallDetailResponse>> GetDetail(int id, CancellationToken cancellationToken)
        => Ok(await _callService.GetDetailAsync(id, cancellationToken));

    [HttpPost]
    [Authorize(Roles = "Agent")]
    public async Task<ActionResult<CallResponse>> Log(LogCallRequest request, CancellationToken cancellationToken)
    {
        await _logValidator.ValidateAndThrowAsync(request, cancellationToken);
        return Ok(await _callService.LogAsync(request, cancellationToken));
    }
}
