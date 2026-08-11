using Microsoft.Extensions.AI;
using Secretary.Agents.Tools;
using Secretary.Domain.Enums;

namespace Secretary.Agents;

/// <summary>Lamiya answering the appointment line: the booking toolset, and the instruction file
/// that has been tuned against real calls since the first one.</summary>
public sealed class AppointmentAgentModule : IAgentModule
{
    private readonly ClientTools _clientTools;
    private readonly ServiceCatalogTools _serviceCatalogTools;
    private readonly AppointmentTools _appointmentTools;
    private readonly EscalationTools _escalationTools;
    private readonly CallControlTools _callControlTools;

    public AppointmentAgentModule(
        ClientTools clientTools, ServiceCatalogTools serviceCatalogTools, AppointmentTools appointmentTools,
        EscalationTools escalationTools, CallControlTools callControlTools)
    {
        _clientTools = clientTools;
        _serviceCatalogTools = serviceCatalogTools;
        _appointmentTools = appointmentTools;
        _escalationTools = escalationTools;
        _callControlTools = callControlTools;
    }

    public Module Key => Module.Appointment;

    public string InstructionName => "Appointment";

    public IList<AITool> BuildTools() => PhoneAgentToolset.Build(
        _clientTools, _serviceCatalogTools, _appointmentTools, _escalationTools, _callControlTools);
}
