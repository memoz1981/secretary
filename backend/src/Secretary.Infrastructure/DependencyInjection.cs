using Secretary.Application.Abstractions;
using Secretary.Application.Abstractions.Persistence;
using Secretary.Infrastructure.Persistence;
using Secretary.Infrastructure.Persistence.Repositories;
using Secretary.Infrastructure.Scheduling;
using Secretary.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;

namespace Secretary.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            // SQL Server until the production deploy, then UseNpgsql and the parked migration
            // in Common/Persistence/Migrations.Postgres. Nothing else in the layer is
            // provider-specific â€” no column types are hardcoded and index filters are
            // double-quoted, which SQL Server, PostgreSQL and SQLite all accept.
            options.UseSqlServer(configuration.GetConnectionString("Default")));

        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<IAccountRepository, AccountRepository>();
        services.AddScoped<IProviderRepository, ProviderRepository>();
        services.AddScoped<IServiceOfferingRepository, ServiceOfferingRepository>();
        services.AddScoped<IProviderServiceOfferingRepository, ProviderServiceOfferingRepository>();
        services.AddScoped<IClientRepository, ClientRepository>();
        services.AddScoped<IAppointmentRepository, AppointmentRepository>();
        services.AddScoped<ICallRepository, CallRepository>();
        services.AddScoped<IEscalationRepository, EscalationRepository>();
        services.AddScoped<ITenantModuleRepository, TenantModuleRepository>();
        services.AddScoped<IBusinessHoursRepository, BusinessHoursRepository>();
        services.AddScoped<IMeasurementUnitRepository, MeasurementUnitRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IOrderSettingsRepository, OrderSettingsRepository>();
        services.AddScoped<IOrderCallRepository, OrderCallRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddSingleton<IClock>(SystemClock.Instance);
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<IOutboundReminderNotifier, LoggingOutboundReminderNotifier>();

        return services;
    }
}

