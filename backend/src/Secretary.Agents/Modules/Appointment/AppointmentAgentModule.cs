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

    /// <summary>How the orchestrator's verdict reads in this module's words.
    ///
    /// ⚠ The orchestrator judges the transport — did the session run to a clean end, or did it
    /// fall over — and it says so in CallOutcome, which Orders records directly. It cannot know
    /// whether the calendar was full, so it never produces NoAvailability; only the booking tools
    /// see that, and until they record it this maps what the orchestrator can actually tell us.
    /// Anything it cannot distinguish becomes Unfinished rather than a guess.</summary>
    private static AppointmentCallOutcome Translate(CallOutcome outcome) => outcome switch
    {
        CallOutcome.ResolvedByAgent => AppointmentCallOutcome.Resolved,
        CallOutcome.EscalatedResolvedByStaff or CallOutcome.EscalatedAbandoned
            => AppointmentCallOutcome.Forwarded,
        CallOutcome.FailedNoAvailability => AppointmentCallOutcome.NoAvailability,

        // NoAnswer included: the shared enum has one and this module has no equivalent, because
        // an inbound line answers by definition. If it ever arrives, it arrived unfinished.
        _ => AppointmentCallOutcome.Unfinished,
    };

    /// <summary>app.Calls, unchanged. The client is resolved inside CallService by phone number,
    /// as it always was — the appointment line has a real number to look one up by.</summary>
    public async Task LogCallAsync(CallLogEntry entry, CancellationToken cancellationToken)
        => await _calls.LogAsync(
            new LogCallRequest(
                _session.ClientId, entry.CallerPhoneNumber, RelatedAppointmentId: null,
                entry.Classification, Translate(entry.Outcome),
                entry.DurationSeconds, entry.TurnCount, entry.CallerTurnCount, WaitTimeSeconds: null,
                entry.RecordingUrl, entry.Transcript, entry.StartedAt, entry.Pipeline, entry.ModelUsages),
            cancellationToken);
}
