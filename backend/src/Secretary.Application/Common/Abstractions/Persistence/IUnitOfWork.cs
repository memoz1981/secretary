namespace Secretary.Application.Abstractions.Persistence;

public interface IUnitOfWork
{
    ITenantRepository Tenants { get; }
    IAccountRepository Accounts { get; }
    IProviderRepository Providers { get; }
    IServiceOfferingRepository ServiceOfferings { get; }
    IProviderServiceOfferingRepository ProviderServiceOfferings { get; }
    IClientRepository Clients { get; }
    IAppointmentRepository Appointments { get; }
    ICallRepository Calls { get; }
    IEscalationRepository Escalations { get; }
    ITenantModuleRepository TenantModules { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
