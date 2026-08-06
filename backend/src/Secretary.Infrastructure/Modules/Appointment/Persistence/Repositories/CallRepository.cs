using Secretary.Application.Abstractions.Persistence;
using Secretary.Domain.Entities;
using Secretary.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace Secretary.Infrastructure.Persistence.Repositories;

internal sealed class CallRepository : ICallRepository
{
    private readonly AppDbContext _db;

    public CallRepository(AppDbContext db) => _db = db;

    public async Task<Call?> GetByIdAsync(int id, CancellationToken cancellationToken)
        => await _db.Calls.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Call>> GetAllAsync(CancellationToken cancellationToken)
        => await _db.Calls.ToListAsync(cancellationToken);

    public async Task AddAsync(Call entity, CancellationToken cancellationToken)
        => await _db.Calls.AddAsync(entity, cancellationToken);

    public void Update(Call entity) => _db.Calls.Update(entity);

    public void Remove(Call entity) => _db.Calls.Remove(entity);

    public async Task<IReadOnlyList<Call>> SearchAsync(
        Instant? fromInclusive,
        Instant? toExclusive,
        CallClassification? classification,
        CallOutcome? outcome,
        int? providerId,
        CallPipeline? pipeline,
        CancellationToken cancellationToken)
    {
        var query = _db.Calls.AsQueryable();

        if (fromInclusive is Instant from)
        {
            query = query.Where(c => c.StartedAt >= from);
        }

        if (toExclusive is Instant to)
        {
            query = query.Where(c => c.StartedAt < to);
        }

        if (classification is CallClassification classificationValue)
        {
            query = query.Where(c => c.Classification == classificationValue);
        }

        if (outcome is CallOutcome outcomeValue)
        {
            query = query.Where(c => c.Outcome == outcomeValue);
        }

        // The filter that makes the four pipelines comparable: same date range, one architecture
        // at a time.
        if (pipeline is CallPipeline pipelineValue)
        {
            query = query.Where(c => c.Pipeline == pipelineValue);
        }

        if (providerId is int providerIdValue)
        {
            var appointmentIdsForProvider = _db.Appointments
                .Where(a => a.ProviderId == providerIdValue)
                .Select(a => (int?)a.Id);

            query = query.Where(c => c.RelatedAppointmentId != null && appointmentIdsForProvider.Contains(c.RelatedAppointmentId));
        }

        return await query.OrderByDescending(c => c.StartedAt).ToListAsync(cancellationToken);
    }
}
