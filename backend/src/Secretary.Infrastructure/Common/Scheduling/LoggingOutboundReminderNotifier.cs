using Secretary.Application.Abstractions;
using Secretary.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Secretary.Infrastructure.Scheduling;

/// <summary>Default implementation — no outbound telephony exists yet, so this just logs
/// clearly rather than silently doing nothing or pretending a call was placed. Swap for a
/// real implementation (see IOutboundReminderNotifier's docstring) once outbound calling
/// exists; the scheduler that calls this doesn't need to change either way.</summary>
internal sealed class LoggingOutboundReminderNotifier : IOutboundReminderNotifier
{
    private readonly ILogger<LoggingOutboundReminderNotifier> _logger;

    public LoggingOutboundReminderNotifier(ILogger<LoggingOutboundReminderNotifier> logger) => _logger = logger;

    public Task NotifyReminderDueAsync(Appointment appointment, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Reminder due for appointment {AppointmentId} (tenant {TenantId}) at {Start} — no outbound calling integration exists yet; this is a placeholder.",
            appointment.Id, appointment.TenantId, appointment.Start);
        return Task.CompletedTask;
    }
}
