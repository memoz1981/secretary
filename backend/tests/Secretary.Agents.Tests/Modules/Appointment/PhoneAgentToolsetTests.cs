using Secretary.Agents.Appointment;
using Secretary.Agents.ServiceCatalog;
using Secretary.Agents.Tools;
using Secretary.Application.Abstractions;
using Secretary.Application.Abstractions.Persistence;
using Secretary.Application.Services;
using Microsoft.Extensions.AI;
using Moq;
using NodaTime;
using Shouldly;
using Xunit;

namespace Secretary.Agents.Tests;

/// <summary>Simple hookup check, not business-logic re-testing (that's already covered in
/// Secretary.Application.Tests, which these tools just call through to) — confirms the
/// toolset assembles the 8 confirmed tools with the names the agent's instructions expect.</summary>
public sealed class PhoneAgentToolsetTests
{
    [Fact]
    public void Build_returns_all_confirmed_tools_by_name()
    {
        var uow = new Mock<IUnitOfWork>();
        var clock = new Mock<IClock>();
        var tenantProvider = new Mock<ICurrentTenantProvider>();
        var catalog = new Mock<ITenantServiceCatalogCache>();
        var providers = new Mock<ITenantProviderDirectory>();

        var clientService = new ClientService(uow.Object, clock.Object, tenantProvider.Object);
        var appointmentService = new AppointmentService(uow.Object, clock.Object, tenantProvider.Object, clientService);
        var escalationService = new EscalationService(uow.Object, clock.Object, tenantProvider.Object, clientService);

        var clientTools = new ClientTools(clientService, new AppointmentCallSession());
        var serviceCatalogTools = new ServiceCatalogTools(catalog.Object);
        var businessHours = new BusinessHoursService(
            new Mock<IBusinessHoursRepository>().Object, uow.Object, clock.Object, tenantProvider.Object,
            new NullAgentDirectoryChangeNotifier());
        var appointmentTools = new AppointmentTools(
            appointmentService, clientService, providers.Object, catalog.Object, businessHours, clock.Object);
        var escalationTools = new EscalationTools(escalationService);
        var callControlTools = new CallControlTools();

        var tools = PhoneAgentToolset.Build(clientTools, serviceCatalogTools, appointmentTools, escalationTools, callControlTools);

        tools.Count.ShouldBe(11);
        tools.OfType<AIFunction>().Select(f => f.Name).ShouldBe(
            new[]
            {
                nameof(ClientTools.LookupCaller),
                nameof(ServiceCatalogTools.GetServiceCatalog),
                nameof(ServiceCatalogTools.GetServiceDetails),
                nameof(AppointmentTools.ListProvidersForService),
                nameof(AppointmentTools.CheckAvailability),
                nameof(AppointmentTools.BookAppointment),
                nameof(AppointmentTools.GetUpcomingAppointments),
                nameof(AppointmentTools.RescheduleAppointment),
                nameof(AppointmentTools.CancelAppointment),
                nameof(EscalationTools.EscalateToHuman),
                nameof(CallControlTools.EndCall),
            },
            ignoreOrder: false);
    }
}
