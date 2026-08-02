using Secretary.Application.Abstractions.Persistence;
using Secretary.Domain.Entities;
using Secretary.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace Secretary.Infrastructure.Persistence.Repositories;

internal sealed class AppointmentRepository : IAppointmentRepository
{
    private readonly AppDbContext _db;

    public AppointmentRepository(AppDbContext db) => _db = db;

    public async Task<Appointment?> GetByIdAsync(int id, CancellationToken cancellationToken)
        => await _db.Appointments.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Appointment>> GetAllAsync(CancellationToken cancellationToken)
        => await _db.Appointments.ToListAsync(cancellationToken);

    public async Task AddAsync(Appointment entity, CancellationToken cancellationToken)
        => await _db.Appointments.AddAsync(entity, cancellationToken);

    public void Update(Appointment entity) => _db.Appointments.Update(entity);

    public void Remove(Appointment entity) => _db.Appointments.Remove(entity);

    public async Task<IReadOnlyList<Appointment>> FindOverlappingAsync(
        int providerId, Instant start, Instant end, CancellationToken cancellationToken)
        => await _db.Appointments
            .Where(a => a.ProviderId == providerId)
            .Where(a => a.Start < end && start < a.End)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Appointment>> GetForDateRangeAsync(
        Instant fromInclusive, Instant toExclusive, int? providerId, CancellationToken cancellationToken)
    {
        var query = _db.Appointments.Where(a => a.Start >= fromInclusive && a.Start < toExclusive);
        if (providerId is not null)
        {
            query = query.Where(a => a.ProviderId == providerId);
        }

        return await query.OrderBy(a => a.Start).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Appointment>> GetUpcomingForClientAsync(int clientId, Instant now, CancellationToken cancellationToken)
        => await _db.Appointments
            .Where(a => a.ClientId == clientId && a.AppointmentStatus != AppointmentStatus.Cancelled && a.Start >= now)
            .OrderBy(a => a.Start)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Appointment>> GetNeedingReminderAsync(
        Instant fromInclusive, Instant toExclusive, CancellationToken cancellationToken)
        => await _db.Appointments
            .Where(a => a.AppointmentStatus == AppointmentStatus.Confirmed && a.Start >= fromInclusive && a.Start < toExclusive)
            .OrderBy(a => a.Start)
            .ToListAsync(cancellationToken);

    public async Task<Appointment?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken)
        => await _db.Appointments.FirstOrDefaultAsync(a => a.IdempotencyKey == idempotencyKey, cancellationToken);

    public async Task<IReadOnlyList<Appointment>> GetNeedingReminderAcrossAllTenantsAsync(
        Instant fromInclusive, Instant toExclusive, CancellationToken cancellationToken)
        => await _db.Appointments
            .IgnoreQueryFilters()
            .Where(a => a.AppointmentStatus == AppointmentStatus.Confirmed && a.Start >= fromInclusive && a.Start < toExclusive)
            .OrderBy(a => a.TenantId).ThenBy(a => a.Start)
            .ToListAsync(cancellationToken);
}
