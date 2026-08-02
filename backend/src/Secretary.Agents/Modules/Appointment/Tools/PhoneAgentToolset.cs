using Microsoft.Extensions.AI;

namespace Secretary.Agents.Tools;

/// <summary>Assembles the full tool list for the inbound/outbound phone agent. A narrower
/// list than "every Application service method" by design — CallService.LogAsync and the
/// reminder-job endpoints are deliberately NOT here; those are orchestration-driven (see
/// PhoneCallOrchestrator), not something the model decides to invoke.</summary>
public static class PhoneAgentToolset
{
    public static IList<AITool> Build(
        ClientTools clientTools, ServiceCatalogTools serviceCatalogTools, AppointmentTools appointmentTools,
        EscalationTools escalationTools, CallControlTools callControlTools)
    {
        return
        [
            AIFunctionFactory.Create(clientTools.LookupCaller),
            AIFunctionFactory.Create(serviceCatalogTools.GetServiceCatalog),
            AIFunctionFactory.Create(serviceCatalogTools.GetServiceDetails),
            AIFunctionFactory.Create(appointmentTools.ListProvidersForService),
            AIFunctionFactory.Create(appointmentTools.CheckAvailability),
            AIFunctionFactory.Create(appointmentTools.BookAppointment),
            AIFunctionFactory.Create(appointmentTools.GetUpcomingAppointments),
            AIFunctionFactory.Create(appointmentTools.RescheduleAppointment),
            AIFunctionFactory.Create(appointmentTools.CancelAppointment),
            AIFunctionFactory.Create(escalationTools.EscalateToHuman),
            AIFunctionFactory.Create(callControlTools.EndCall),
        ];
    }
}
