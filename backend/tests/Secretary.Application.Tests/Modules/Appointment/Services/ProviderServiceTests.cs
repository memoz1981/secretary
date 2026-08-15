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

public sealed class ProviderServiceTests
{
    private const int TenantId = 7;
    private static readonly Instant Now = Instant.FromUtc(2026, 7, 11, 9, 0);

    private readonly FakeUnitOfWork _uow = new();
    private readonly ProviderService _sut;

    public ProviderServiceTests()
    {
        _sut = new ProviderService(_uow.Object, new FakeClock(Now), new FakeCurrentTenantProvider(TenantId), new NullAgentDirectoryChangeNotifier());
    }

    [Fact]
    public async Task ListAsync_returns_providers_for_current_tenant()
    {
        var provider = Provider.Create(TenantId, "Rasim (chair 1)", Now);
        _uow.Providers.Setup(p => p.GetAllForCurrentTenantAsync(default)).ReturnsAsync([provider]);

        var result = await _sut.ListAsync(default);

        result.Count.ShouldBe(1);
        result[0].Name.ShouldBe("Rasim (chair 1)");
    }

    [Fact]
    public async Task CreateAsync_requires_a_tenant_scoped_caller()
    {
        var sut = new ProviderService(_uow.Object, new FakeClock(Now), new FakeCurrentTenantProvider(null), new NullAgentDirectoryChangeNotifier());

        await Should.ThrowAsync<InvalidOperationException>(() => sut.CreateAsync(new CreateProviderRequest("Rasim"), default));
    }

    [Fact]
    public async Task CreateAsync_adds_provider_and_assigns_every_offering()
    {
        var haircut = ServiceOffering.Create(TenantId, "Haircut", 15m, 30, Now);
        var shave = ServiceOffering.Create(TenantId, "Shave", 10m, 15, Now);
        _uow.ServiceOfferings.Setup(s => s.GetAllForCurrentTenantAsync(default)).ReturnsAsync([haircut, shave]);

        var result = await _sut.CreateAsync(new CreateProviderRequest("Rasim (chair 1)"), default);

        result.Name.ShouldBe("Rasim (chair 1)");
        _uow.Providers.Verify(p => p.AddAsync(It.IsAny<Provider>(), default), Times.Once);
        _uow.ProviderServiceOfferings.Verify(
            m => m.AddAsync(It.IsAny<ProviderServiceOffering>(), default), Times.Exactly(2));

        // Saved twice: once so the provider's identity Id exists, once for the matrix rows.
        _uow.UnitOfWork.Verify(u => u.SaveChangesAsync(default), Times.Exactly(2));
    }

    [Fact]
    public async Task CreateAsync_with_no_offerings_saves_once()
    {
        _uow.ServiceOfferings.Setup(s => s.GetAllForCurrentTenantAsync(default)).ReturnsAsync([]);

        await _sut.CreateAsync(new CreateProviderRequest("Rasim (chair 1)"), default);

        _uow.ProviderServiceOfferings.Verify(
            m => m.AddAsync(It.IsAny<ProviderServiceOffering>(), default), Times.Never);
        _uow.UnitOfWork.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task ListForOfferingAsync_returns_only_providers_actively_offering_the_service()
    {
        var rasim = Provider.Create(TenantId, "Rasim", Now);
        var elvin = Provider.Create(TenantId, "Elvin", Now);
        _uow.Providers.Setup(p => p.GetAllForCurrentTenantAsync(default)).ReturnsAsync([rasim, elvin]);

        // Only rasim's id (0 for both here — so return an empty list to prove filtering).
        _uow.ProviderServiceOfferings
            .Setup(m => m.GetActiveProviderIdsForOfferingAsync(30, default))
            .ReturnsAsync([]);

        var result = await _sut.ListForOfferingAsync(30, default);

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task UpdateAsync_throws_not_found_when_missing()
    {
        _uow.Providers.Setup(p => p.GetByIdAsync(It.IsAny<int>(), default)).ReturnsAsync((Provider?)null);

        await Should.ThrowAsync<NotFoundException>(() => _sut.UpdateAsync(42, new UpdateProviderRequest("Name"), default));
    }

    [Fact]
    public async Task RemoveAsync_throws_when_provider_has_upcoming_appointments()
    {
        var provider = Provider.Create(TenantId, "Rasim (chair 1)", Now);
        _uow.Providers.Setup(p => p.GetByIdAsync(provider.Id, default)).ReturnsAsync(provider);

        var appointment = Appointment.Create(
            TenantId, 10, provider.Id, 30,
            Instant.FromUtc(2026, 7, 12, 9, 0), Instant.FromUtc(2026, 7, 12, 9, 30), null, AppointmentCreatedBy.Staff,
            Now);

        // From now, not from the beginning of time — see the second test below for why.
        _uow.Appointments
            .Setup(a => a.GetForDateRangeAsync(Now, Instant.MaxValue, provider.Id, default))
            .ReturnsAsync([appointment]);

        await Should.ThrowAsync<InvalidStateTransitionException>(() => _sut.RemoveAsync(provider.Id, default));
    }

    [Fact]
    public async Task RemoveAsync_soft_deactivates_provider_with_no_upcoming_appointments()
    {
        var provider = Provider.Create(TenantId, "Rasim (chair 1)", Now);
        _uow.Providers.Setup(p => p.GetByIdAsync(provider.Id, default)).ReturnsAsync(provider);
        _uow.Appointments
            .Setup(a => a.GetForDateRangeAsync(Now, Instant.MaxValue, provider.Id, default))
            .ReturnsAsync([]);

        await _sut.RemoveAsync(provider.Id, default);

        provider.Status.ShouldBe(EntityStatus.Inactive);
        _uow.Providers.Verify(p => p.Remove(It.IsAny<Provider>()), Times.Never);
        _uow.UnitOfWork.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    /// <summary>Only work still to come may refuse a removal.
    ///
    /// ⚠ The check used to span Instant.MinValue to Instant.MaxValue — every appointment that
    /// had ever existed — so one completed haircut made a provider permanently undeletable, and
    /// a tenant running for a season could remove nobody who had ever worked for them. The name
    /// said "upcoming" and the range said "ever". This pins the range, because that disagreement
    /// is invisible from the outside: both versions throw, just on different days.</summary>
    [Fact]
    public async Task RemoveAsync_only_asks_about_appointments_still_to_come()
    {
        var provider = Provider.Create(TenantId, "Rasim (chair 1)", Now);
        _uow.Providers.Setup(p => p.GetByIdAsync(provider.Id, default)).ReturnsAsync(provider);
        _uow.Appointments
            .Setup(a => a.GetForDateRangeAsync(It.IsAny<Instant>(), It.IsAny<Instant>(), provider.Id, default))
            .ReturnsAsync([]);

        await _sut.RemoveAsync(provider.Id, default);

        _uow.Appointments.Verify(
            a => a.GetForDateRangeAsync(Now, Instant.MaxValue, provider.Id, default), Times.Once);

        _uow.Appointments.Verify(
            a => a.GetForDateRangeAsync(Instant.MinValue, It.IsAny<Instant>(), It.IsAny<int?>(), default),
            Times.Never);
    }

    [Fact]
    public async Task GetServiceMatrixAsync_builds_a_cell_for_every_provider_offering_pair()
    {
        var provider = Provider.Create(TenantId, "Rasim", Now);
        var haircut = ServiceOffering.Create(TenantId, "Haircut", 15m, 30, Now);
        var shave = ServiceOffering.Create(TenantId, "Shave", 10m, 15, Now);
        _uow.Providers.Setup(p => p.GetAllForCurrentTenantAsync(default)).ReturnsAsync([provider]);
        _uow.ServiceOfferings.Setup(s => s.GetAllForCurrentTenantAsync(default)).ReturnsAsync([haircut, shave]);
        _uow.ProviderServiceOfferings.Setup(m => m.GetAllForCurrentTenantAsync(default)).ReturnsAsync([]);

        var result = await _sut.GetServiceMatrixAsync(default);

        result.Providers.Count.ShouldBe(1);
        result.ServiceOfferings.Count.ShouldBe(2);
        result.Assignments.Count.ShouldBe(2);
        result.Assignments.ShouldAllBe(a => !a.Active);
    }

    [Fact]
    public async Task SetServiceAssignmentAsync_deactivates_an_existing_active_row()
    {
        var provider = Provider.Create(TenantId, "Rasim", Now);
        var offering = ServiceOffering.Create(TenantId, "Haircut", 15m, 30, Now);
        var assignment = ProviderServiceOffering.Create(TenantId, provider.Id, offering.Id, Now);

        _uow.Providers.Setup(p => p.GetByIdAsync(provider.Id, default)).ReturnsAsync(provider);
        _uow.ServiceOfferings.Setup(s => s.GetByIdAsync(offering.Id, default)).ReturnsAsync(offering);
        _uow.ProviderServiceOfferings
            .Setup(m => m.GetByProviderAndOfferingAsync(provider.Id, offering.Id, default))
            .ReturnsAsync(assignment);

        await _sut.SetServiceAssignmentAsync(
            provider.Id, new SetProviderServiceAssignmentRequest(offering.Id, Active: false), default);

        assignment.Status.ShouldBe(EntityStatus.Inactive);
        _uow.ProviderServiceOfferings.Verify(m => m.AddAsync(It.IsAny<ProviderServiceOffering>(), default), Times.Never);
        _uow.UnitOfWork.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task SetServiceAssignmentAsync_creates_a_missing_row()
    {
        var provider = Provider.Create(TenantId, "Rasim", Now);
        var offering = ServiceOffering.Create(TenantId, "Haircut", 15m, 30, Now);

        _uow.Providers.Setup(p => p.GetByIdAsync(provider.Id, default)).ReturnsAsync(provider);
        _uow.ServiceOfferings.Setup(s => s.GetByIdAsync(offering.Id, default)).ReturnsAsync(offering);
        _uow.ProviderServiceOfferings
            .Setup(m => m.GetByProviderAndOfferingAsync(provider.Id, offering.Id, default))
            .ReturnsAsync((ProviderServiceOffering?)null);

        await _sut.SetServiceAssignmentAsync(
            provider.Id, new SetProviderServiceAssignmentRequest(offering.Id, Active: true), default);

        _uow.ProviderServiceOfferings.Verify(m => m.AddAsync(It.IsAny<ProviderServiceOffering>(), default), Times.Once);
        _uow.UnitOfWork.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task SetServiceAssignmentAsync_throws_when_offering_missing()
    {
        var provider = Provider.Create(TenantId, "Rasim", Now);
        _uow.Providers.Setup(p => p.GetByIdAsync(provider.Id, default)).ReturnsAsync(provider);
        _uow.ServiceOfferings.Setup(s => s.GetByIdAsync(It.IsAny<int>(), default)).ReturnsAsync((ServiceOffering?)null);

        await Should.ThrowAsync<NotFoundException>(() => _sut.SetServiceAssignmentAsync(
            provider.Id, new SetProviderServiceAssignmentRequest(42, Active: true), default));
    }
}
