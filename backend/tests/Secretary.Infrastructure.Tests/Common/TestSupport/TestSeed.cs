using Secretary.Domain.Entities;
using Secretary.Infrastructure.Persistence;
using NodaTime;

namespace Secretary.Infrastructure.Tests.TestSupport;

/// <summary>Real FK constraints (which Microsoft.Data.Sqlite enforces) mean every child row
/// needs its parent rows first — this seeds the minimal tenant graph tests hang rows off.
/// Identity ids only exist after SaveChanges, hence the save inside each helper.</summary>
public static class TestSeed
{
    private static readonly Instant Now = Instant.FromUtc(2026, 7, 11, 8, 0);

    public static Tenant AddTenant(AppDbContext context, string name = "Test Tenant")
    {
        var tenant = Tenant.Create(name, "Asia/Baku", null, showCallCosts: false, Now);
        context.Tenants.Add(tenant);
        context.SaveChanges();
        return tenant;
    }

    public static Client AddClient(AppDbContext context, int tenantId, string phone = "+994000000")
    {
        var client = Client.Create(tenantId, phone, Now);
        context.Clients.Add(client);
        context.SaveChanges();
        return client;
    }

    public static Provider AddProvider(AppDbContext context, int tenantId, string name = "Rasim")
    {
        var provider = Provider.Create(tenantId, name, Now);
        context.Providers.Add(provider);
        context.SaveChanges();
        return provider;
    }

    public static ServiceOffering AddOffering(AppDbContext context, int tenantId, string name = "Haircut")
    {
        var offering = ServiceOffering.Create(tenantId, name, 15m, 30, Now);
        context.ServiceOfferings.Add(offering);
        context.SaveChanges();
        return offering;
    }
}
