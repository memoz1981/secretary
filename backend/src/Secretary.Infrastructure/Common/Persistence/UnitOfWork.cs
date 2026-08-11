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
        ITenantModuleRepository tenantModules,
        IBusinessHoursRepository businessHours,
        IMeasurementUnitRepository units,
        IProductRepository products,
        ICustomerRepository customers,
        IOrderRepository orders,
        IOrderSettingsRepository orderSettings)
    {
        _db = db;
        Units = units;
        Products = products;
        Customers = customers;
        Orders = orders;
        OrderSettings = orderSettings;
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
        BusinessHours = businessHours;
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
    public IBusinessHoursRepository BusinessHours { get; }
    public IMeasurementUnitRepository Units { get; }
    public IProductRepository Products { get; }
    public ICustomerRepository Customers { get; }
    public IOrderRepository Orders { get; }
    public IOrderSettingsRepository OrderSettings { get; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => _db.SaveChangesAsync(cancellationToken);
}
