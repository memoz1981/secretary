using System.Security.Claims;
using Secretary.Application.Dtos;
using Secretary.Application.Services;
using Secretary.Domain.Enums;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NodaTime;
using Secretary.Api.Auth;

namespace Secretary.Api.Controllers;

/// <summary>Calendar + Appointment Panel (Owner/Staff, Flow F) and the same endpoints under
/// an Agent-role JWT for the AI voice agent (Flows A/B/C, agent-developer's consumer).</summary>
[ApiController]
[RequireModule(Module.Appointment)]
[Authorize(Roles = "Owner,Staff,Agent")]
[Route("api/appointments")]
public sealed class AppointmentsController : ControllerBase
{
    private readonly AppointmentService _appointmentService;
    private readonly IValidator<CreateAppointmentRequest> _createValidator;
    private readonly IValidator<RescheduleAppointmentRequest> _rescheduleValidator;
    private readonly IValidator<AvailabilityRequest> _availabilityValidator;

    public AppointmentsController(
        AppointmentService appointmentService,
        IValidator<CreateAppointmentRequest> createValidator,
        IValidator<RescheduleAppointmentRequest> rescheduleValidator,
        IValidator<AvailabilityRequest> availabilityValidator)
    {
        _appointmentService = appointmentService;
        _createValidator = createValidator;
        _rescheduleValidator = rescheduleValidator;
        _availabilityValidator = availabilityValidator;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AppointmentResponse>>> List(
        [FromQuery] Instant from, [FromQuery] Instant to, [FromQuery] int? providerId, CancellationToken cancellationToken)
        => Ok(await _appointmentService.ListAsync(from, to, providerId, cancellationToken));

    [HttpGet("availability")]
    public async Task<ActionResult<AvailabilityResponse>> FindAvailability([FromQuery] AvailabilityRequest request, CancellationToken cancellationToken)
    {
        await _availabilityValidator.ValidateAndThrowAsync(request, cancellationToken);
        return Ok(await _appointmentService.FindAvailabilityAsync(request, cancellationToken));
    }

    [HttpGet("reminders/today")]
    [Authorize(Roles = "Agent")]
    public async Task<ActionResult<IReadOnlyList<AppointmentResponse>>> GetTodaysReminders(
        [FromQuery] Instant from, [FromQuery] Instant to, CancellationToken cancellationToken)
        => Ok(await _appointmentService.GetNeedingReminderAsync(from, to, cancellationToken));

    [HttpPost]
    public async Task<ActionResult<AppointmentResponse>> Create(CreateAppointmentRequest request, CancellationToken cancellationToken)
    {
        await _createValidator.ValidateAndThrowAsync(request, cancellationToken);
        var createdBy = CallerRole() == AccountRole.Agent ? AppointmentCreatedBy.Agent : AppointmentCreatedBy.Staff;
        return Ok(await _appointmentService.CreateAsync(request, createdBy, cancellationToken));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<AppointmentResponse>> Reschedule([FromRoute] int id, RescheduleAppointmentRequest request, CancellationToken cancellationToken)
    {
        await _rescheduleValidator.ValidateAndThrowAsync(request, cancellationToken);
        return Ok(await _appointmentService.RescheduleAsync(id, request, cancellationToken));
    }

    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id, CancellationToken cancellationToken)
    {
        await _appointmentService.CancelAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:int}/reminder-confirmed")]
    [Authorize(Roles = "Agent")]
    public async Task<IActionResult> ConfirmReminder(int id, CancellationToken cancellationToken)
    {
        await _appointmentService.ConfirmReminderAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:int}/reminder-no-answer")]
    [Authorize(Roles = "Agent")]
    public async Task<IActionResult> MarkReminderNoAnswer(int id, CancellationToken cancellationToken)
    {
        await _appointmentService.MarkReminderNoAnswerAsync(id, cancellationToken);
        return NoContent();
    }

    // MapInboundClaims is off (Program.cs), so the identity carries the short "role" key —
    // reading the long ClaimTypes.Role URI here returned null and broke every web booking.
    private AccountRole CallerRole() => Enum.Parse<AccountRole>(User.FindFirstValue(Auth.AppClaimTypes.Role)!);
}
