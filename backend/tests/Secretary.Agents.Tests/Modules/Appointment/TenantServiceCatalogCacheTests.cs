using Secretary.Agents.ServiceCatalog;
using Secretary.Application.Abstractions;
using Secretary.Application.Abstractions.Persistence;
using Secretary.Application.Services;
using Secretary.Domain.Entities;
using Moq;
using NodaTime;
using NodaTime.Testing;
using Xunit;

namespace Secretary.Agents.Tests;

/// <summary>Flow G's core requirement: don't query on every call, only on cold start or an
/// explicit invalidation. Simple hookup test, not a re-test of ServiceOfferingService itself.</summary>
public sealed class TenantServiceCatalogCacheTests
{
    [Fact]
    public async Task GetCatalogAsync_only_loads_from_the_service_once_until_invalidated()
    {
        const int tenantId = 7;
        var uow = new Mock<IUnitOfWork>();
        var serviceOfferings = new Mock<IServiceOfferingRepository>();
        uow.SetupGet(u => u.ServiceOfferings).Returns(serviceOfferings.Object);
        serviceOfferings
            .Setup(s => s.GetAllForCurrentTenantAsync(default))
            .ReturnsAsync([ServiceOffering.Create(tenantId, "Haircut", 15m, 30, Instant.FromUtc(2026, 7, 11, 9, 0))]);

        var tenantProvider = new FixedTenantProvider(tenantId);
        var serviceOfferingService = new ServiceOfferingService(uow.Object, new FakeClock(Instant.FromUtc(2026, 7, 11, 9, 0)), tenantProvider, new NullAgentDirectoryChangeNotifier());
        var cache = new TenantServiceCatalogCache(serviceOfferingService, tenantProvider);

        await cache.GetCatalogAsync(default);
        await cache.GetCatalogAsync(default);

        serviceOfferings.Verify(s => s.GetAllForCurrentTenantAsync(default), Times.Once);

        new AgentDirectoryChangeNotifier().NotifyChanged(tenantId);
        await cache.GetCatalogAsync(default);

        serviceOfferings.Verify(s => s.GetAllForCurrentTenantAsync(default), Times.Exactly(2));
    }

    private sealed class FixedTenantProvider : ICurrentTenantProvider
    {
        public FixedTenantProvider(int tenantId) => TenantId = tenantId;
        public int? TenantId { get; }
    }
}
