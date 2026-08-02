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

public sealed class AppointmentServiceTests
{
    private static readonly Instant Now = Instant.FromUtc(2026, 7, 11, 8, 0);
    private const int TenantId = 7;
    private const int ProviderId = 20;
    private const int ServiceOfferingId = 30;

    private readonly FakeUnitOfWork _uow = new();
    private readonly AppointmentService _sut;

    public AppointmentServiceTests()
    {
        var tenantProvider = new FakeCurrentTenantProvider(TenantId);
        var clientService = new ClientService(_uow.Object, new FakeClock(Now), tenantProvider);
        _sut = new AppointmentService(_uow.Object, new FakeClock(Now), tenantProvider, clientService);

        _uow.Clients.Setup(c => c.GetByPhoneNumberAsync(It.IsAny<string>(), default)).ReturnsAsync((Client?)null);

        // The provider offers every service unless a test says otherwise.
        _uow.ProviderServiceOfferings
            .Setup(m => m.GetByProviderAndOfferingAsync(It.IsAny<int>(), It.IsAny<int>(), default))
            .ReturnsAsync((int p, int s, CancellationToken _) => ProviderServiceOffering.Create(TenantId, p, s, Now));
    }

    private static ServiceOffering ThirtyMinuteService() => ServiceOffering.Create(TenantId, "Haircut", 15m, 30, Now);

    [Fact]
    public async Task FindAvailabilityAsync_returns_back_to_back_slots_when_nothing_booked()
    {
        var service = ThirtyMinuteService();
        _uow.ServiceOfferings.Setup(s => s.GetByIdAsync(service.Id, default)).ReturnsAsync(service);

        var from = Instant.FromUtc(2026, 7, 11, 9, 0);
        var to = Instant.FromUtc(2026, 7, 11, 10, 0);
        _uow.Appointments.Setup(a => a.FindOverlappingAsync(ProviderId, from, to, default)).ReturnsAsync([]);

        var result = await _sut.FindAvailabilityAsync(new AvailabilityRequest(ProviderId, service.Id, from, to), default);

        result.Slots.Count.ShouldBe(2);
        result.Slots[0].Start.ShouldBe(from);
        result.Slots[0].End.ShouldBe(from + Duration.FromMinutes(30));
        result.Slots[1].Start.ShouldBe(from + Duration.FromMinutes(30));
        result.Slots[1].End.ShouldBe(to);
    }

    [Fact]
    public async Task FindAvailabilityAsync_excludes_slots_that_overlap_an_existing_appointment()
    {
        var service = ThirtyMinuteService();
        _uow.ServiceOfferings.Setup(s => s.GetByIdAsync(service.Id, default)).ReturnsAsync(service);

        var from = Instant.FromUtc(2026, 7, 11, 9, 0);
        var to = Instant.FromUtc(2026, 7, 11, 10, 0);
        var existing = Appointment.Create(
            TenantId, 10, ProviderId, service.Id, from, from + Duration.FromMinutes(30), null, AppointmentCreatedBy.Staff, Now);

        _uow.Appointments.Setup(a => a.FindOverlappingAsync(ProviderId, from, to, default)).ReturnsAsync([existing]);

        var result = await _sut.FindAvailabilityAsync(new AvailabilityRequest(ProviderId, service.Id, from, to), default);

        result.Slots.Count.ShouldBe(1);
        result.Slots[0].Start.ShouldBe(from + Duration.FromMinutes(30));
    }

    [Fact]
    public async Task FindAvailabilityAsync_ignores_cancelled_appointments()
    {
        var service = ThirtyMinuteService();
        _uow.ServiceOfferings.Setup(s => s.GetByIdAsync(service.Id, default)).ReturnsAsync(service);

        var from = Instant.FromUtc(2026, 7, 11, 9, 0);
        var to = Instant.FromUtc(2026, 7, 11, 9, 30);
        var cancelled = Appointment.Create(
            TenantId, 10, ProviderId, service.Id, from, from + Duration.FromMinutes(30), null, AppointmentCreatedBy.Staff, Now);
        cancelled.Cancel(Now);

        _uow.Appointments.Setup(a => a.FindOverlappingAsync(ProviderId, from, to, default)).ReturnsAsync([cancelled]);

        var result = await _sut.FindAvailabilityAsync(new AvailabilityRequest(ProviderId, service.Id, from, to), default);

        result.Slots.Count.ShouldBe(1);
    }

    [Fact]
    public async Task FindAvailabilityAsync_throws_not_found_when_service_offering_missing()
    {
        _uow.ServiceOfferings.Setup(s => s.GetByIdAsync(It.IsAny<int>(), default)).ReturnsAsync((ServiceOffering?)null);

        await Should.ThrowAsync<NotFoundException>(() => _sut.FindAvailabilityAsync(
            new AvailabilityRequest(ProviderId, 42, Now, Now + Duration.FromHours(1)), default));
    }

    [Fact]
    public async Task FindAvailabilityAsync_throws_when_provider_does_not_offer_the_service()
    {
        var service = ThirtyMinuteService();
        _uow.ServiceOfferings.Setup(s => s.GetByIdAsync(service.Id, default)).ReturnsAsync(service);
        _uow.ProviderServiceOfferings
            .Setup(m => m.GetByProviderAndOfferingAsync(ProviderId, service.Id, default))
            .ReturnsAsync((ProviderServiceOffering?)null);

        await Should.ThrowAsync<ProviderDoesNotOfferServiceException>(() => _sut.FindAvailabilityAsync(
            new AvailabilityRequest(ProviderId, service.Id, Now, Now + Duration.FromHours(1)), default));
    }

    [Fact]
    public async Task CreateAsync_books_appointment_and_creates_client_when_none_exists()
    {
        var start = Instant.FromUtc(2026, 7, 11, 9, 0);
        var end = start + Duration.FromMinutes(30);
        _uow.Appointments.Setup(a => a.FindOverlappingAsync(ProviderId, start, end, default)).ReturnsAsync([]);

        var request = new CreateAppointmentRequest("+994000000", "Tofiq Aliyev", ProviderId, ServiceOfferingId, start, end, null);
        var result = await _sut.CreateAsync(request, AppointmentCreatedBy.Agent, default);

        result.ClientPhoneNumber.ShouldBe("+994000000");
        result.CreatedBy.ShouldBe(AppointmentCreatedBy.Agent);
        _uow.Appointments.Verify(a => a.AddAsync(It.IsAny<Appointment>(), default), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_throws_when_provider_does_not_offer_the_service()
    {
        var start = Instant.FromUtc(2026, 7, 11, 9, 0);
        var end = start + Duration.FromMinutes(30);
        var inactive = ProviderServiceOffering.Create(TenantId, ProviderId, ServiceOfferingId, Now);
        inactive.Deactivate(Now);
        _uow.ProviderServiceOfferings
            .Setup(m => m.GetByProviderAndOfferingAsync(ProviderId, ServiceOfferingId, default))
            .ReturnsAsync(inactive);

        var request = new CreateAppointmentRequest("+994000000", "Tofiq Aliyev", ProviderId, ServiceOfferingId, start, end, null);

        await Should.ThrowAsync<ProviderDoesNotOfferServiceException>(() => _sut.CreateAsync(request, AppointmentCreatedBy.Agent, default));
        _uow.Appointments.Verify(a => a.AddAsync(It.IsAny<Appointment>(), default), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_throws_when_client_is_blacklisted()
    {
        var start = Instant.FromUtc(2026, 7, 11, 9, 0);
        var end = start + Duration.FromMinutes(30);
        _uow.Appointments.Setup(a => a.FindOverlappingAsync(ProviderId, start, end, default)).ReturnsAsync([]);

        var client = Client.Create(TenantId, "+994000000", Now, "Tofiq Aliyev");
        client.BlackList("repeated no-shows", Now);
        _uow.Clients.Setup(c => c.GetByPhoneNumberAsync("+994000000", default)).ReturnsAsync(client);

        var request = new CreateAppointmentRequest("+994000000", "Tofiq Aliyev", ProviderId, ServiceOfferingId, start, end, null);

        await Should.ThrowAsync<ClientBlackListedException>(() => _sut.CreateAsync(request, AppointmentCreatedBy.Agent, default));
        _uow.Appointments.Verify(a => a.AddAsync(It.IsAny<Appointment>(), default), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_with_a_previously_used_idempotency_key_returns_the_existing_appointment_without_creating_a_duplicate()
    {
        var start = Instant.FromUtc(2026, 7, 11, 9, 0);
        var end = start + Duration.FromMinutes(30);
        var existingClient = Client.Create(TenantId, "+994000000", Now, "Tofiq Aliyev");
        var existingAppointment = Appointment.Create(
            TenantId, existingClient.Id, ProviderId, ServiceOfferingId, start, end, null, AppointmentCreatedBy.Agent, Now, "retry-key-123");

        _uow.Appointments.Setup(a => a.GetByIdempotencyKeyAsync("retry-key-123", default)).ReturnsAsync(existingAppointment);
        _uow.Clients.Setup(c => c.GetByIdAsync(existingClient.Id, default)).ReturnsAsync(existingClient);

        var request = new CreateAppointmentRequest("+994000000", "Tofiq Aliyev", ProviderId, ServiceOfferingId, start, end, null, "retry-key-123");
        var result = await _sut.CreateAsync(request, AppointmentCreatedBy.Agent, default);

        result.Id.ShouldBe(existingAppointment.Id);
        _uow.Appointments.Verify(a => a.AddAsync(It.IsAny<Appointment>(), default), Times.Never);
        _uow.Appointments.Verify(a => a.FindOverlappingAsync(It.IsAny<int>(), It.IsAny<Instant>(), It.IsAny<Instant>(), default), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_with_a_new_idempotency_key_books_normally()
    {
        var start = Instant.FromUtc(2026, 7, 11, 9, 0);
        var end = start + Duration.FromMinutes(30);
        _uow.Appointments.Setup(a => a.GetByIdempotencyKeyAsync("brand-new-key", default)).ReturnsAsync((Appointment?)null);
        _uow.Appointments.Setup(a => a.FindOverlappingAsync(ProviderId, start, end, default)).ReturnsAsync([]);

        var request = new CreateAppointmentRequest("+994000000", "Tofiq Aliyev", ProviderId, ServiceOfferingId, start, end, null, "brand-new-key");
        await _sut.CreateAsync(request, AppointmentCreatedBy.Agent, default);

        _uow.Appointments.Verify(a => a.AddAsync(It.IsAny<Appointment>(), default), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_throws_scheduling_conflict_when_slot_overlaps()
    {
        var start = Instant.FromUtc(2026, 7, 11, 9, 0);
        var end = start + Duration.FromMinutes(30);
        var existing = Appointment.Create(TenantId, 10, ProviderId, ServiceOfferingId, start, end, null, AppointmentCreatedBy.Staff, Now);
        _uow.Appointments.Setup(a => a.FindOverlappingAsync(ProviderId, start, end, default)).ReturnsAsync([existing]);

        var request = new CreateAppointmentRequest("+994000000", "Tofiq Aliyev", ProviderId, ServiceOfferingId, start, end, null);

        await Should.ThrowAsync<SchedulingConflictException>(() => _sut.CreateAsync(request, AppointmentCreatedBy.Staff, default));
    }

    [Fact]
    public async Task RescheduleAsync_updates_appointment_when_new_slot_is_free()
    {
        var oldStart = Instant.FromUtc(2026, 7, 11, 9, 0);
        var appointment = Appointment.Create(
            TenantId, 10, ProviderId, ServiceOfferingId, oldStart, oldStart + Duration.FromMinutes(30), null, AppointmentCreatedBy.Staff, Now);
        _uow.Appointments.Setup(a => a.GetByIdAsync(appointment.Id, default)).ReturnsAsync(appointment);

        var newStart = Instant.FromUtc(2026, 7, 11, 11, 0);
        var newEnd = newStart + Duration.FromMinutes(30);
        _uow.Appointments.Setup(a => a.FindOverlappingAsync(ProviderId, newStart, newEnd, default)).ReturnsAsync([]);
        _uow.Clients.Setup(c => c.GetByIdAsync(appointment.ClientId, default)).ReturnsAsync((Client?)null);

        var result = await _sut.RescheduleAsync(appointment.Id, new RescheduleAppointmentRequest(ProviderId, ServiceOfferingId, newStart, newEnd), default);

        result.Start.ShouldBe(newStart);
        result.End.ShouldBe(newEnd);
    }

    [Fact]
    public async Task RescheduleAsync_excludes_the_appointment_being_rescheduled_from_its_own_overlap_check()
    {
        var start = Instant.FromUtc(2026, 7, 11, 9, 0);
        var end = start + Duration.FromMinutes(30);
        var appointment = Appointment.Create(TenantId, 10, ProviderId, ServiceOfferingId, start, end, null, AppointmentCreatedBy.Staff, Now);
        _uow.Appointments.Setup(a => a.GetByIdAsync(appointment.Id, default)).ReturnsAsync(appointment);

        // Same slot as itself — should NOT be a conflict since we exclude our own id.
        _uow.Appointments.Setup(a => a.FindOverlappingAsync(ProviderId, start, end, default)).ReturnsAsync([appointment]);
        _uow.Clients.Setup(c => c.GetByIdAsync(appointment.ClientId, default)).ReturnsAsync((Client?)null);

        var result = await _sut.RescheduleAsync(appointment.Id, new RescheduleAppointmentRequest(ProviderId, ServiceOfferingId, start, end), default);

        result.Start.ShouldBe(start);
    }

    [Fact]
    public async Task CancelAsync_marks_appointment_cancelled()
    {
        var start = Instant.FromUtc(2026, 7, 11, 9, 0);
        var appointment = Appointment.Create(TenantId, 10, ProviderId, ServiceOfferingId, start, start + Duration.FromMinutes(30), null, AppointmentCreatedBy.Staff, Now);
        _uow.Appointments.Setup(a => a.GetByIdAsync(appointment.Id, default)).ReturnsAsync(appointment);

        await _sut.CancelAsync(appointment.Id, default);

        appointment.AppointmentStatus.ShouldBe(AppointmentStatus.Cancelled);
    }

    [Fact]
    public async Task MarkReminderNoAnswerAsync_flags_appointment()
    {
        var start = Instant.FromUtc(2026, 7, 11, 9, 0);
        var appointment = Appointment.Create(TenantId, 10, ProviderId, ServiceOfferingId, start, start + Duration.FromMinutes(30), null, AppointmentCreatedBy.Agent, Now);
        _uow.Appointments.Setup(a => a.GetByIdAsync(appointment.Id, default)).ReturnsAsync(appointment);

        await _sut.MarkReminderNoAnswerAsync(appointment.Id, default);

        appointment.ReminderNoAnswerFlag.ShouldBeTrue();
    }

    [Fact]
    public async Task ConfirmReminderAsync_clears_flag()
    {
        var start = Instant.FromUtc(2026, 7, 11, 9, 0);
        var appointment = Appointment.Create(TenantId, 10, ProviderId, ServiceOfferingId, start, start + Duration.FromMinutes(30), null, AppointmentCreatedBy.Agent, Now);
        appointment.MarkReminderNoAnswer(Now);
        _uow.Appointments.Setup(a => a.GetByIdAsync(appointment.Id, default)).ReturnsAsync(appointment);

        await _sut.ConfirmReminderAsync(appointment.Id, default);

        appointment.ReminderNoAnswerFlag.ShouldBeFalse();
    }
}
