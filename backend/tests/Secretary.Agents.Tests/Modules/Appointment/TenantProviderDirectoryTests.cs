using Secretary.Agents.ServiceCatalog;
using Secretary.Application.Abstractions;
using Secretary.Application.Abstractions.Persistence;
using Secretary.Application.Services;
using Secretary.Domain.Entities;
using Moq;
using NodaTime;
using NodaTime.Testing;
using Shouldly;
using Xunit;

namespace Secretary.Agents.Tests;

/// <summary>"Who performs this service" is asked several times per booking and only changes
/// when the Owner edits the Providers page — so it is read once and then remembered, and the
/// moment that page changes it must be forgotten. Getting the second half wrong means telling a
/// caller that someone can cut their hair when the business has said otherwise.</summary>
public sealed class TenantProviderDirectoryTests
{
    private const int TenantId = 11;
    private const int ServiceOfferingId = 3;
    private static readonly Instant Now = Instant.FromUtc(2026, 7, 26, 9, 0);

    [Fact]
    public async Task The_providers_for_a_service_are_only_read_once_until_invalidated()
    {
        var (directory, providers) = BuildDirectory();

        await directory.GetProvidersForServiceAsync(ServiceOfferingId, default);
        await directory.GetProvidersForServiceAsync(ServiceOfferingId, default);

        providers.Verify(p => p.GetAllForCurrentTenantAsync(default), Times.Once);

        new AgentDirectoryChangeNotifier().NotifyChanged(TenantId);
        await directory.GetProvidersForServiceAsync(ServiceOfferingId, default);

        providers.Verify(p => p.GetAllForCurrentTenantAsync(default), Times.Exactly(2));
    }

    [Fact]
    public async Task Each_service_is_remembered_separately()
    {
        var (directory, providers) = BuildDirectory();

        await directory.GetProvidersForServiceAsync(ServiceOfferingId, default);
        await directory.GetProvidersForServiceAsync(ServiceOfferingId + 1, default);

        // Different services have different provider lists — one cached answer must not stand
        // in for the other.
        providers.Verify(p => p.GetAllForCurrentTenantAsync(default), Times.Exactly(2));
    }

    [Fact]
    public async Task Another_tenants_change_leaves_this_tenant_alone()
    {
        var (directory, providers) = BuildDirectory();

        await directory.GetProvidersForServiceAsync(ServiceOfferingId, default);
        new AgentDirectoryChangeNotifier().NotifyChanged(TenantId + 1);
        await directory.GetProvidersForServiceAsync(ServiceOfferingId, default);

        providers.Verify(p => p.GetAllForCurrentTenantAsync(default), Times.Once);
    }

    [Fact]
    public async Task The_cached_answer_is_the_real_one()
    {
        var (directory, _) = BuildDirectory();

        var result = await directory.GetProvidersForServiceAsync(ServiceOfferingId, default);

        result.Select(p => p.Name).ShouldBe(["Ferid"]);
    }

    /// <summary>Each test gets its own tenant-free slate: the store behind the directory is
    /// process-wide by design, so leftovers from a previous test would hide a cache miss.</summary>
    private static (ITenantProviderDirectory Directory, Mock<IProviderRepository> Providers) BuildDirectory()
    {
        ProviderDirectoryStore.Invalidate(TenantId);
        ProviderDirectoryStore.Invalidate(TenantId + 1);

        var uow = new Mock<IUnitOfWork>();
        var providers = new Mock<IProviderRepository>();
        var assignments = new Mock<IProviderServiceOfferingRepository>();
        uow.SetupGet(u => u.Providers).Returns(providers.Object);
        uow.SetupGet(u => u.ProviderServiceOfferings).Returns(assignments.Object);

        var ferid = Provider.Create(TenantId, "Ferid", Now);
        providers.Setup(p => p.GetAllForCurrentTenantAsync(default)).ReturnsAsync([ferid]);
        assignments
            .Setup(a => a.GetActiveProviderIdsForOfferingAsync(It.IsAny<int>(), default))
            .ReturnsAsync([ferid.Id]);

        var tenantProvider = new FixedTenant(TenantId);
        var providerService = new ProviderService(
            uow.Object, new FakeClock(Now), tenantProvider, new NullAgentDirectoryChangeNotifier());

        return (new TenantProviderDirectory(providerService, tenantProvider), providers);
    }

    private sealed class FixedTenant : ICurrentTenantProvider
    {
        public FixedTenant(int tenantId) => TenantId = tenantId;
        public int? TenantId { get; }
    }
}
