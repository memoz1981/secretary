using Secretary.Domain.Entities;
using Secretary.Infrastructure.Tests.TestSupport;
using NodaTime;
using Shouldly;
using Xunit;

namespace Secretary.Infrastructure.Tests.Persistence;

/// <summary>Proves the global tenant query filter actually isolates tenants at the EF Core
/// level — the single most safety-critical piece of this multi-tenant backend, so it's
/// worth a real (non-mocked) test against a real provider rather than trusting the LINQ
/// expression by inspection.</summary>
public sealed class TenantQueryFilterTests
{
    private static readonly Instant Now = Instant.FromUtc(2026, 7, 11, 9, 0);

    [Fact]
    public async Task Providers_are_invisible_across_tenants()
    {
        using var factory = new SqliteDbContextFactory();
        int tenantA;
        int tenantB;
        using (var seedContext = factory.CreateContext(null))
        {
            tenantA = TestSeed.AddTenant(seedContext, "Tenant A").Id;
            tenantB = TestSeed.AddTenant(seedContext, "Tenant B").Id;
            seedContext.Providers.Add(Provider.Create(tenantA, "Rasim (chair 1)", Now));
            seedContext.Providers.Add(Provider.Create(tenantB, "Kamran (chair 2)", Now));
            await seedContext.SaveChangesAsync();
        }

        using var tenantAView = factory.CreateContext(tenantA);
        var visibleToTenantA = tenantAView.Providers.ToList();

        visibleToTenantA.Count.ShouldBe(1);
        visibleToTenantA[0].Name.ShouldBe("Rasim (chair 1)");
    }

    [Fact]
    public async Task Platform_admin_context_sees_no_tenant_scoped_rows()
    {
        using var factory = new SqliteDbContextFactory();
        using (var seedContext = factory.CreateContext(null))
        {
            var tenant = TestSeed.AddTenant(seedContext).Id;
            seedContext.Providers.Add(Provider.Create(tenant, "Rasim (chair 1)", Now));
            await seedContext.SaveChangesAsync();
        }

        using var platformAdminView = factory.CreateContext(null);
        platformAdminView.Providers.ToList().ShouldBeEmpty();
    }

    [Fact]
    public async Task ProviderServiceOfferings_are_invisible_across_tenants()
    {
        using var factory = new SqliteDbContextFactory();
        int tenantA;
        int tenantB;
        using (var seedContext = factory.CreateContext(null))
        {
            tenantA = TestSeed.AddTenant(seedContext, "Tenant A").Id;
            tenantB = TestSeed.AddTenant(seedContext, "Tenant B").Id;
            var provider = TestSeed.AddProvider(seedContext, tenantA);
            var offering = TestSeed.AddOffering(seedContext, tenantA);
            seedContext.ProviderServiceOfferings.Add(
                ProviderServiceOffering.Create(tenantA, provider.Id, offering.Id, Now));
            await seedContext.SaveChangesAsync();
        }

        using var tenantBView = factory.CreateContext(tenantB);
        tenantBView.ProviderServiceOfferings.ToList().ShouldBeEmpty();

        using var tenantAView = factory.CreateContext(tenantA);
        tenantAView.ProviderServiceOfferings.ToList().Count.ShouldBe(1);
    }

    [Fact]
    public async Task Accounts_are_not_tenant_filtered_since_login_must_search_across_tenants()
    {
        using var factory = new SqliteDbContextFactory();
        using (var seedContext = factory.CreateContext(null))
        {
            var tenant = TestSeed.AddTenant(seedContext).Id;
            seedContext.Accounts.Add(Account.CreateOwner(tenant, "Elvin", "elvin@business.az", "hash", Now));
            await seedContext.SaveChangesAsync();
        }

        using var platformAdminView = factory.CreateContext(null);
        platformAdminView.Accounts.ToList().Count.ShouldBe(1);
    }
}
