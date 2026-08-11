using Secretary.Application.Abstractions.Persistence;
using Moq;

namespace Secretary.Application.Tests.TestSupport;

/// <summary>Bundles a mocked repository per aggregate plus a mocked IUnitOfWork wired to
/// return them, so each service test only sets up the repositories it actually touches.</summary>
public sealed class FakeUnitOfWork
{
    public Mock<ITenantRepository> Tenants { get; } = new();
    public Mock<IAccountRepository> Accounts { get; } = new();
    public Mock<IProviderRepository> Providers { get; } = new();
    public Mock<IServiceOfferingRepository> ServiceOfferings { get; } = new();
    public Mock<IProviderServiceOfferingRepository> ProviderServiceOfferings { get; } = new();
    public Mock<IClientRepository> Clients { get; } = new();
    public Mock<IAppointmentRepository> Appointments { get; } = new();
    public Mock<ICallRepository> Calls { get; } = new();
    public Mock<IEscalationRepository> Escalations { get; } = new();
    public Mock<ITenantModuleRepository> TenantModules { get; } = new();
    public Mock<IBusinessHoursRepository> BusinessHours { get; } = new();

    public Mock<IUnitOfWork> UnitOfWork { get; }

    public FakeUnitOfWork()
    {
        UnitOfWork = new Mock<IUnitOfWork>();
        UnitOfWork.SetupGet(u => u.Tenants).Returns(Tenants.Object);
        UnitOfWork.SetupGet(u => u.Accounts).Returns(Accounts.Object);
        UnitOfWork.SetupGet(u => u.Providers).Returns(Providers.Object);
        UnitOfWork.SetupGet(u => u.ServiceOfferings).Returns(ServiceOfferings.Object);
        UnitOfWork.SetupGet(u => u.ProviderServiceOfferings).Returns(ProviderServiceOfferings.Object);
        UnitOfWork.SetupGet(u => u.Clients).Returns(Clients.Object);
        UnitOfWork.SetupGet(u => u.Appointments).Returns(Appointments.Object);
        UnitOfWork.SetupGet(u => u.Calls).Returns(Calls.Object);
        UnitOfWork.SetupGet(u => u.Escalations).Returns(Escalations.Object);
        UnitOfWork.SetupGet(u => u.TenantModules).Returns(TenantModules.Object);
        UnitOfWork.SetupGet(u => u.BusinessHours).Returns(BusinessHours.Object);

        // Moq returns null for an unconfigured Task-returning method, which every caller of this
        // one then dereferences. Defaulted to "no modules" so a test that does not care about
        // modules does not have to say so.
        TenantModules
            .Setup(r => r.GetEnabledModulesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
    }

    public IUnitOfWork Object => UnitOfWork.Object;
}
