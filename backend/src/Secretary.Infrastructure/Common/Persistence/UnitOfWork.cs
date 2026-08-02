using Secretary.Application.Abstractions.Persistence;

namespace Secretary.Infrastructure.Persistence;

internal sealed class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _db;

    public UnitOfWork(
        AppDbContext db,
        ITenantRepository tenants,
        IAccountRepository accounts,
        IProviderRepository providers,
        IServiceOfferingRepository serviceOfferings,
        IProviderServiceOfferingRepository providerServiceOfferings,
        IClientRepository clients,
        IAppointmentRepository appointments,
        ICallRepository calls,
        IEscalationRepository escalations,
        ITenantModuleRepository tenantModules)
    {
        _db = db;
        Tenants = tenants;
        Accounts = accounts;
        Providers = providers;
        ServiceOfferings = serviceOfferings;
        ProviderServiceOfferings = providerServiceOfferings;
        Clients = clients;
        Appointments = appointments;
        Calls = calls;
        Escalations = escalations;
        TenantModules = tenantModules;
    }

    public ITenantRepository Tenants { get; }
    public IAccountRepository Accounts { get; }
    public IProviderRepository Providers { get; }
    public IServiceOfferingRepository ServiceOfferings { get; }
    public IProviderServiceOfferingRepository ProviderServiceOfferings { get; }
    public IClientRepository Clients { get; }
    public IAppointmentRepository Appointments { get; }
    public ICallRepository Calls { get; }
    public IEscalationRepository Escalations { get; }
    public ITenantModuleRepository TenantModules { get; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => _db.SaveChangesAsync(cancellationToken);
}
