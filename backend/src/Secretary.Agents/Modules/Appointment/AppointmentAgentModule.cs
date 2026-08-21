using Microsoft.Extensions.AI;
using Secretary.Agents.Tools;
using Secretary.Application.Dtos;
using Secretary.Application.Services;
using Secretary.Domain.Enums;

namespace Secretary.Agents;

/// <summary>Lamiya answering the appointment line: the booking toolset, and the instruction file
/// that has been tuned against real calls since the first one.</summary>
public sealed class AppointmentAgentModule : IAgentModule
{
    private readonly ClientTools _clientTools;
    private readonly Appointment.AppointmentCallSession _session;
    private readonly ServiceCatalogTools _serviceCatalogTools;
    private readonly AppointmentTools _appointmentTools;
    private readonly EscalationTools _escalationTools;
    private readonly CallControlTools _callControlTools;
    private readonly CallService _calls;

    public AppointmentAgentModule(
        ClientTools clientTools, Appointment.AppointmentCallSession session,
        ServiceCatalogTools serviceCatalogTools, AppointmentTools appointmentTools,
        EscalationTools escalationTools, CallControlTools callControlTools, CallService calls)
    {
        _clientTools = clientTools;
        _session = session;
        _serviceCatalogTools = serviceCatalogTools;
        _appointmentTools = appointmentTools;
        _escalationTools = escalationTools;
        _callControlTools = callControlTools;
        _calls = calls;
    }

    public Module Key => Module.Appointment;

    public string InstructionName => "Appointment";

    public IList<AITool> BuildTools() => PhoneAgentToolset.Build(
        _clientTools, _serviceCatalogTools, _appointmentTools, _escalationTools, _callControlTools);

    /// <summary>app.Calls, unchanged. The client is resolved inside CallService by phone number,
    /// as it always was — the appointment line has a real number to look one up by.</summary>
    public async Task LogCallAsync(CallLogEntry entry, CancellationToken cancellationToken)
        => await _calls.LogAsync(
            new LogCallRequest(
                _session.ClientId, entry.CallerPhoneNumber, RelatedAppointmentId: null,
                entry.Classification, entry.Outcome,
                entry.DurationSeconds, entry.TurnCount, entry.CallerTurnCount, WaitTimeSeconds: null,
                entry.RecordingUrl, entry.Transcript, entry.StartedAt, entry.Pipeline, entry.ModelUsages),
            cancellationToken);
}
