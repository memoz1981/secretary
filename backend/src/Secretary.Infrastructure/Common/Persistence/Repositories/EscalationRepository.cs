using Secretary.Application.Abstractions.Persistence;
using Secretary.Domain.Entities;
using Secretary.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace Secretary.Infrastructure.Persistence.Repositories;

internal sealed class EscalationRepository : IEscalationRepository
{
    private readonly AppDbContext _db;

    public EscalationRepository(AppDbContext db) => _db = db;

    public async Task<Escalation?> GetByIdAsync(int id, CancellationToken cancellationToken)
        => await _db.Escalations.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Escalation>> GetAllAsync(CancellationToken cancellationToken)
        => await _db.Escalations.ToListAsync(cancellationToken);

    public async Task AddAsync(Escalation entity, CancellationToken cancellationToken)
        => await _db.Escalations.AddAsync(entity, cancellationToken);

    public void Update(Escalation entity) => _db.Escalations.Update(entity);

    public void Remove(Escalation entity) => _db.Escalations.Remove(entity);

    public async Task<IReadOnlyList<Escalation>> GetRingingForCurrentTenantAsync(CancellationToken cancellationToken)
        => await _db.Escalations
            .Where(e => e.EscalationStatus == EscalationStatus.Ringing)
            .OrderBy(e => e.RaisedAt)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Escalation>> GetRingingOlderThanAcrossAllTenantsAsync(Instant cutoff, CancellationToken cancellationToken)
        => await _db.Escalations
            .IgnoreQueryFilters()
            .Where(e => e.EscalationStatus == EscalationStatus.Ringing && e.RaisedAt <= cutoff)
            .ToListAsync(cancellationToken);
}
