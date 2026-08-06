using Secretary.Application.Abstractions;
using Secretary.Domain.Entities;
using Secretary.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;

namespace Secretary.Infrastructure.Persistence;

public sealed class AppDbContext : DbContext
{
    private readonly ICurrentTenantProvider _currentTenant;

    public AppDbContext(DbContextOptions<AppDbContext> options, ICurrentTenantProvider currentTenant)
        : base(options)
    {
        _currentTenant = currentTenant;
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Provider> Providers => Set<Provider>();
    public DbSet<ServiceOffering> ServiceOfferings => Set<ServiceOffering>();
    public DbSet<ProviderServiceOffering> ProviderServiceOfferings => Set<ProviderServiceOffering>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<Call> Calls => Set<Call>();
    public DbSet<Escalation> Escalations => Set<Escalation>();
    public DbSet<TenantModule> TenantModules => Set<TenantModule>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<NodaTime.Instant>().HaveConversion<InstantConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Tenant isolation: every tenant-scoped entity is invisible outside its own tenant,
        // enforced here rather than trusted to every call site. A caller with no tenant
        // claim (platform admin) sees none of these rows — that domain only ever touches
        // Tenant and Account, which are deliberately not filtered here (Account's own note
        // explains why: login has to search across tenants).
        modelBuilder.Entity<Provider>().HasQueryFilter(p => p.TenantId == _currentTenant.TenantId);
        modelBuilder.Entity<ServiceOffering>().HasQueryFilter(s => s.TenantId == _currentTenant.TenantId);
        modelBuilder.Entity<ProviderServiceOffering>().HasQueryFilter(p => p.TenantId == _currentTenant.TenantId);
        modelBuilder.Entity<Client>().HasQueryFilter(c => c.TenantId == _currentTenant.TenantId);
        modelBuilder.Entity<Appointment>().HasQueryFilter(a => a.TenantId == _currentTenant.TenantId);
        modelBuilder.Entity<Call>().HasQueryFilter(c => c.TenantId == _currentTenant.TenantId);
        modelBuilder.Entity<Escalation>().HasQueryFilter(e => e.TenantId == _currentTenant.TenantId);

        // TenantModule is deliberately absent from this list. The platform admin who grants and
        // revokes module access has no TenantId at all, so a filter of the shape above would
        // return nothing for the one account allowed to administer the table — it would look
        // like no tenant had ever been granted anything. Scoping is explicit in
        // TenantModuleRepository, where every method takes the tenant as an argument.
    }
}
