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
    public DbSet<BusinessHours> BusinessHours => Set<BusinessHours>();

    // Orders module.
    public DbSet<OrderSettings> OrderSettings => Set<OrderSettings>();
    public DbSet<MeasurementUnit> Units => Set<MeasurementUnit>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<CustomerPhoneNumber> CustomerPhoneNumbers => Set<CustomerPhoneNumber>();
    public DbSet<CustomerAddress> CustomerAddresses => Set<CustomerAddress>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderLine> OrderLines => Set<OrderLine>();
    public DbSet<OrderCall> OrderCalls => Set<OrderCall>();

    // Feedback module.
    public DbSet<Survey> Surveys => Set<Survey>();
    public DbSet<SurveyQuestion> SurveyQuestions => Set<SurveyQuestion>();
    public DbSet<SurveyQuestionOption> SurveyQuestionOptions => Set<SurveyQuestionOption>();
    public DbSet<SurveyRequest> SurveyRequests => Set<SurveyRequest>();
    public DbSet<FeedbackCall> FeedbackCalls => Set<FeedbackCall>();
    public DbSet<FeedbackAnswer> FeedbackAnswers => Set<FeedbackAnswer>();
    public DbSet<FeedbackSettings> FeedbackSettings => Set<FeedbackSettings>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<NodaTime.Instant>().HaveConversion<InstantConverter>();
        configurationBuilder.Properties<NodaTime.LocalDate>().HaveConversion<LocalDateConverter>();
        configurationBuilder.Properties<NodaTime.LocalTime>().HaveConversion<LocalTimeConverter>();
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
        modelBuilder.Entity<Product>().HasQueryFilter(p => p.TenantId == _currentTenant.TenantId);
        modelBuilder.Entity<Customer>().HasQueryFilter(c => c.TenantId == _currentTenant.TenantId);
        modelBuilder.Entity<Order>().HasQueryFilter(o => o.TenantId == _currentTenant.TenantId);
        modelBuilder.Entity<BusinessHours>().HasQueryFilter(h => h.TenantId == _currentTenant.TenantId);
        modelBuilder.Entity<OrderSettings>().HasQueryFilter(s => s.TenantId == _currentTenant.TenantId);
        modelBuilder.Entity<OrderCall>().HasQueryFilter(c => c.TenantId == _currentTenant.TenantId);
        modelBuilder.Entity<Survey>().HasQueryFilter(s => s.TenantId == _currentTenant.TenantId);
        modelBuilder.Entity<SurveyRequest>().HasQueryFilter(r => r.TenantId == _currentTenant.TenantId);
        modelBuilder.Entity<FeedbackCall>().HasQueryFilter(c => c.TenantId == _currentTenant.TenantId);
        modelBuilder.Entity<FeedbackSettings>().HasQueryFilter(s => s.TenantId == _currentTenant.TenantId);

        // Deliberately unfiltered: a customer's phone numbers and addresses, and the order lines
        // under an order. None carries a TenantId, and giving them one to filter on would mean
        // two places for the same fact to disagree. They are only ever reached through the row
        // that is filtered — which makes the rule "never query these without their parent" a
        // repository discipline rather than something the schema enforces. Units are not tenant
        // data at all: a kilogram is a kilogram.

        // TenantModule is deliberately absent from this list. The platform admin who grants and
        // revokes module access has no TenantId at all, so a filter of the shape above would
        // return nothing for the one account allowed to administer the table — it would look
        // like no tenant had ever been granted anything. Scoping is explicit in
        // TenantModuleRepository, where every method takes the tenant as an argument.
    }
}
