using Secretary.Application.Abstractions;
using Secretary.Application.Dtos;
using Secretary.Application.Services;
using Secretary.Application.Tests.TestSupport;
using Secretary.Domain.Entities;
using Secretary.Domain.Enums;
using Secretary.Domain.Exceptions;
using Moq;
using NodaTime;
using NodaTime.Testing;
using Shouldly;
using Xunit;

namespace Secretary.Application.Tests.Services;

public sealed class ServiceOfferingServiceTests
{
    private static readonly Instant Now = Instant.FromUtc(2026, 7, 11, 9, 0);
    private const int TenantId = 7;

    private readonly FakeUnitOfWork _uow = new();
    private readonly Mock<IAgentDirectoryChangeNotifier> _changeNotifier = new();
    private readonly ServiceOfferingService _sut;

    public ServiceOfferingServiceTests()
    {
        _uow.Providers.Setup(p => p.GetAllForCurrentTenantAsync(default)).ReturnsAsync([]);
        _sut = new ServiceOfferingService(_uow.Object, new FakeClock(Now), new FakeCurrentTenantProvider(TenantId), _changeNotifier.Object);
    }

    [Fact]
    public async Task CreateAsync_notifies_the_catalog_change_notifier()
    {
        await _sut.CreateAsync(new CreateServiceOfferingRequest("Haircut", 15m, 30), default);

        _changeNotifier.Verify(n => n.NotifyChanged(TenantId), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_assigns_the_new_offering_to_every_provider()
    {
        var rasim = Provider.Create(TenantId, "Rasim", Now);
        var elvin = Provider.Create(TenantId, "Elvin", Now);
        _uow.Providers.Setup(p => p.GetAllForCurrentTenantAsync(default)).ReturnsAsync([rasim, elvin]);

        await _sut.CreateAsync(new CreateServiceOfferingRequest("Haircut", 15m, 30), default);

        _uow.ProviderServiceOfferings.Verify(
            m => m.AddAsync(It.IsAny<ProviderServiceOffering>(), default), Times.Exactly(2));

        // Saved twice: once so the offering's identity Id exists, once for the matrix rows.
        _uow.UnitOfWork.Verify(u => u.SaveChangesAsync(default), Times.Exactly(2));
    }

    [Fact]
    public async Task UpdateAsync_notifies_the_catalog_change_notifier()
    {
        var service = ServiceOffering.Create(TenantId, "Haircut", 15m, 30, Now);
        _uow.ServiceOfferings.Setup(s => s.GetByIdAsync(service.Id, default)).ReturnsAsync(service);

        await _sut.UpdateAsync(service.Id, new UpdateServiceOfferingRequest("Haircut deluxe", 20m, 40), default);

        _changeNotifier.Verify(n => n.NotifyChanged(TenantId), Times.Once);
    }

    [Fact]
    public async Task RemoveAsync_soft_deactivates_and_notifies_the_catalog_change_notifier()
    {
        var service = ServiceOffering.Create(TenantId, "Haircut", 15m, 30, Now);
        _uow.ServiceOfferings.Setup(s => s.GetByIdAsync(service.Id, default)).ReturnsAsync(service);

        await _sut.RemoveAsync(service.Id, default);

        service.Status.ShouldBe(EntityStatus.Inactive);
        _uow.ServiceOfferings.Verify(s => s.Remove(It.IsAny<ServiceOffering>()), Times.Never);
        _changeNotifier.Verify(n => n.NotifyChanged(TenantId), Times.Once);
    }

    [Fact]
    public async Task ListAsync_returns_services_for_current_tenant()
    {
        var service = ServiceOffering.Create(TenantId, "Haircut", 15m, 30, Now);
        _uow.ServiceOfferings.Setup(s => s.GetAllForCurrentTenantAsync(default)).ReturnsAsync([service]);

        var result = await _sut.ListAsync(default);

        result.Count.ShouldBe(1);
        result[0].Name.ShouldBe("Haircut");
    }

    [Fact]
    public async Task CreateAsync_adds_service_and_saves()
    {
        var result = await _sut.CreateAsync(new CreateServiceOfferingRequest("Haircut", 15m, 30), default);

        result.Name.ShouldBe("Haircut");
        result.UpdatedAt.ShouldBe(Now);
        _uow.ServiceOfferings.Verify(s => s.AddAsync(It.IsAny<ServiceOffering>(), default), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_bumps_updated_at()
    {
        var service = ServiceOffering.Create(TenantId, "Haircut", 15m, 30, Now);
        _uow.ServiceOfferings.Setup(s => s.GetByIdAsync(service.Id, default)).ReturnsAsync(service);

        var result = await _sut.UpdateAsync(service.Id, new UpdateServiceOfferingRequest("Haircut deluxe", 20m, 40), default);

        result.Name.ShouldBe("Haircut deluxe");
        result.Price.ShouldBe(20m);
    }

    [Fact]
    public async Task RemoveAsync_throws_not_found_when_missing()
    {
        _uow.ServiceOfferings.Setup(s => s.GetByIdAsync(It.IsAny<int>(), default)).ReturnsAsync((ServiceOffering?)null);

        await Should.ThrowAsync<NotFoundException>(() => _sut.RemoveAsync(42, default));
    }
}
