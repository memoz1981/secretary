using Secretary.Domain.Entities;
using NodaTime;

namespace Secretary.Application.Abstractions.Persistence;

public interface IAppointmentRepository : IRepository<Appointment>
{
    Task<IReadOnlyList<Appointment>> FindOverlappingAsync(
        int providerId, Instant start, Instant end, CancellationToken cancellationToken);

    Task<IReadOnlyList<Appointment>> GetForDateRangeAsync(
        Instant fromInclusive, Instant toExclusive, int? providerId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Appointment>> GetUpcomingForClientAsync(int clientId, Instant now, CancellationToken cancellationToken);

    /// <summary>Confirmed appointments starting within [fromInclusive, toExclusive) for the
    /// current tenant — backs the 9am reminder job's "today's appointments" endpoint (Flow E).</summary>
    Task<IReadOnlyList<Appointment>> GetNeedingReminderAsync(
        Instant fromInclusive, Instant toExclusive, CancellationToken cancellationToken);

    /// <summary>Backs idempotent booking — null if no appointment was created with this key yet.</summary>
    Task<Appointment?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken);

    /// <summary>Deliberately crosses every tenant, bypassing the usual tenant scoping — backs
    /// the Hangfire reminder scheduler (Flow E), which is a system job with no single ambient
    /// tenant, same reasoning as IEscalationRepository's equivalent cross-tenant method.</summary>
    Task<IReadOnlyList<Appointment>> GetNeedingReminderAcrossAllTenantsAsync(
        Instant fromInclusive, Instant toExclusive, CancellationToken cancellationToken);
}
