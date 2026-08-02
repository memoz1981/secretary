using Secretary.Application.Abstractions;
using Secretary.Application.Abstractions.Persistence;
using Secretary.Application.Dtos;
using Secretary.Domain.Entities;
using Secretary.Domain.Enums;
using Secretary.Domain.Exceptions;
using NodaTime;

namespace Secretary.Application.Services;

/// <summary>Calendar + Appointment Panel (Owner/Staff, Flow F) and, via the same methods,
/// whatever the AI voice agent calls under its Agent-role JWT (Flows A/B/C — see
/// handoff.md). One implementation, two callers.</summary>
public sealed class AppointmentService
{
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;
    private readonly ICurrentTenantProvider _currentTenant;
    private readonly ClientService _clientService;

    public AppointmentService(IUnitOfWork uow, IClock clock, ICurrentTenantProvider currentTenant, ClientService clientService)
    {
        _uow = uow;
        _clock = clock;
        _currentTenant = currentTenant;
        _clientService = clientService;
    }

    public async Task<AvailabilityResponse> FindAvailabilityAsync(AvailabilityRequest request, CancellationToken cancellationToken)
    {
        var service = await _uow.ServiceOfferings.GetByIdAsync(request.ServiceOfferingId, cancellationToken)
            ?? throw new NotFoundException(nameof(ServiceOffering), request.ServiceOfferingId);

        await EnsureProviderOffersServiceAsync(request.ProviderId, request.ServiceOfferingId, cancellationToken);

        var duration = Duration.FromMinutes(service.DurationMinutes);
        var existing = await _uow.Appointments.FindOverlappingAsync(request.ProviderId, request.From, request.To, cancellationToken);
        var active = existing.Where(a => a.AppointmentStatus != AppointmentStatus.Cancelled).ToList();

        // A window that starts in the past (e.g. the agent checking "today" mid-morning) must
        // not produce slots that have already gone by — clamp to now, rounded up to the next
        // full hour: a caller at 10:39 gets offered 11:00, not 10:40.
        var now = _clock.GetCurrentInstant();
        var cursor = request.From;
        if (cursor < now)
        {
            const long stepMs = 60 * 60 * 1000;
            var nowMs = now.ToUnixTimeMilliseconds();
            cursor = Instant.FromUnixTimeMilliseconds((nowMs + stepMs - 1) / stepMs * stepMs);
        }

        var windowEnd = request.To;
        var slots = new List<AvailableSlot>();

        while (cursor + duration <= windowEnd)
        {
            var slotEnd = cursor + duration;
            var overlaps = active.Any(a => a.Start < slotEnd && cursor < a.End);
            if (!overlaps)
            {
                slots.Add(new AvailableSlot(cursor, slotEnd));
            }

            cursor += duration;
        }

        return new AvailabilityResponse(slots);
    }

    public async Task<AppointmentResponse> CreateAsync(CreateAppointmentRequest request, AppointmentCreatedBy createdBy, CancellationToken cancellationToken)
    {
        var tenantId = RequireTenant();

        // Idempotent replay: a retried request with the same key resolves to the appointment
        // that request already created, rather than double-booking or conflicting with itself.
        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            var existing = await _uow.Appointments.GetByIdempotencyKeyAsync(request.IdempotencyKey, cancellationToken);
            if (existing is not null)
            {
                var existingClient = await _uow.Clients.GetByIdAsync(existing.ClientId, cancellationToken);
                return ToResponse(existing, existingClient);
            }
        }

        await EnsureProviderOffersServiceAsync(request.ProviderId, request.ServiceOfferingId, cancellationToken);
        await EnsureNoOverlapAsync(request.ProviderId, request.Start, request.End, cancellationToken);

        var client = await _clientService.FindOrCreateByPhoneNumberAsync(request.ClientPhoneNumber, request.ClientName, cancellationToken);
        if (client.BlackListed)
        {
            throw new ClientBlackListedException(client.PhoneNumber);
        }

        var appointment = Appointment.Create(
            tenantId, client.Id, request.ProviderId, request.ServiceOfferingId,
            request.Start, request.End, request.Notes, createdBy, _clock.GetCurrentInstant(), request.IdempotencyKey);

        await _uow.Appointments.AddAsync(appointment, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);
        return ToResponse(appointment, client);
    }

    public async Task<AppointmentResponse> RescheduleAsync(int id, RescheduleAppointmentRequest request, CancellationToken cancellationToken)
    {
        var appointment = await GetOrThrowAsync(id, cancellationToken);
        await EnsureProviderOffersServiceAsync(request.ProviderId, request.ServiceOfferingId, cancellationToken);
        await EnsureNoOverlapAsync(request.ProviderId, request.Start, request.End, cancellationToken, excluding: id);

        appointment.Reschedule(request.ProviderId, request.ServiceOfferingId, request.Start, request.End, _clock.GetCurrentInstant());
        await _uow.SaveChangesAsync(cancellationToken);

        var client = await _uow.Clients.GetByIdAsync(appointment.ClientId, cancellationToken);
        return ToResponse(appointment, client);
    }

    public async Task CancelAsync(int id, CancellationToken cancellationToken)
    {
        var appointment = await GetOrThrowAsync(id, cancellationToken);
        appointment.Cancel(_clock.GetCurrentInstant());
        await _uow.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AppointmentResponse>> ListAsync(
        Instant from, Instant to, int? providerId, CancellationToken cancellationToken)
    {
        var appointments = await _uow.Appointments.GetForDateRangeAsync(from, to, providerId, cancellationToken);
        var responses = new List<AppointmentResponse>(appointments.Count);
        foreach (var appointment in appointments)
        {
            var client = await _uow.Clients.GetByIdAsync(appointment.ClientId, cancellationToken);
            responses.Add(ToResponse(appointment, client));
        }

        return responses;
    }

    /// <summary>Backs the Agent-only reminder endpoint (Flow E) — today's confirmed
    /// appointments the 9am job should place outbound calls for.</summary>
    public async Task<IReadOnlyList<AppointmentResponse>> GetNeedingReminderAsync(
        Instant fromInclusive, Instant toExclusive, CancellationToken cancellationToken)
    {
        var appointments = await _uow.Appointments.GetNeedingReminderAsync(fromInclusive, toExclusive, cancellationToken);
        var responses = new List<AppointmentResponse>(appointments.Count);
        foreach (var appointment in appointments)
        {
            var client = await _uow.Clients.GetByIdAsync(appointment.ClientId, cancellationToken);
            responses.Add(ToResponse(appointment, client));
        }

        return responses;
    }

    public async Task MarkReminderNoAnswerAsync(int id, CancellationToken cancellationToken)
    {
        var appointment = await GetOrThrowAsync(id, cancellationToken);
        appointment.MarkReminderNoAnswer(_clock.GetCurrentInstant());
        await _uow.SaveChangesAsync(cancellationToken);
    }

    public async Task ConfirmReminderAsync(int id, CancellationToken cancellationToken)
    {
        var appointment = await GetOrThrowAsync(id, cancellationToken);
        appointment.ConfirmReminder(_clock.GetCurrentInstant());
        await _uow.SaveChangesAsync(cancellationToken);
    }

    /// <summary>A provider that doesn't actively offer the requested service must not be
    /// bookable for it — the same rule the agent's provider list already applies, enforced
    /// here so no caller can bypass it.</summary>
    private async Task EnsureProviderOffersServiceAsync(int providerId, int serviceOfferingId, CancellationToken cancellationToken)
    {
        var assignment = await _uow.ProviderServiceOfferings.GetByProviderAndOfferingAsync(
            providerId, serviceOfferingId, cancellationToken);

        if (assignment is null || assignment.Status != EntityStatus.Active)
        {
            throw new ProviderDoesNotOfferServiceException(providerId, serviceOfferingId);
        }
    }

    private async Task EnsureNoOverlapAsync(
        int providerId, Instant start, Instant end, CancellationToken cancellationToken, int? excluding = null)
    {
        var overlapping = await _uow.Appointments.FindOverlappingAsync(providerId, start, end, cancellationToken);
        var conflict = overlapping.Any(a => a.AppointmentStatus != AppointmentStatus.Cancelled && a.Id != excluding);
        if (conflict)
        {
            throw new SchedulingConflictException(providerId, start, end);
        }
    }

    private async Task<Appointment> GetOrThrowAsync(int id, CancellationToken cancellationToken)
        => await _uow.Appointments.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException(nameof(Appointment), id);

    private int RequireTenant()
        => _currentTenant.TenantId ?? throw new InvalidOperationException("This operation requires a tenant-scoped caller.");

    private static AppointmentResponse ToResponse(Appointment appointment, Client? client)
        => new(
            appointment.Id, appointment.ClientId, client?.PhoneNumber ?? string.Empty, client?.Name,
            appointment.ProviderId, appointment.ServiceOfferingId, appointment.Start, appointment.End,
            appointment.AppointmentStatus, appointment.ReminderNoAnswerFlag, appointment.Notes, appointment.CreatedBy);
}
