using Secretary.Application.Abstractions.Persistence;
using Secretary.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Secretary.Infrastructure.Persistence.Repositories;

internal sealed class BusinessHoursRepository : IBusinessHoursRepository
{
    private readonly AppDbContext _db;

    public BusinessHoursRepository(AppDbContext db) => _db = db;

    public async Task<BusinessHours?> GetByIdAsync(int id, CancellationToken cancellationToken)
        => await _db.BusinessHours.FirstOrDefaultAsync(h => h.Id == id, cancellationToken);

    public async Task<IReadOnlyList<BusinessHours>> GetAllAsync(CancellationToken cancellationToken)
        => await _db.BusinessHours.ToListAsync(cancellationToken);

    public async Task AddAsync(BusinessHours entity, CancellationToken cancellationToken)
        => await _db.BusinessHours.AddAsync(entity, cancellationToken);

    public void Update(BusinessHours entity) => _db.BusinessHours.Update(entity);

    public void Remove(BusinessHours entity) => _db.BusinessHours.Remove(entity);

    /// <summary>Tenant-scoped by the query filter, so this is the whole week for the caller's own
    /// business.</summary>
    public async Task<IReadOnlyList<BusinessHours>> GetWeekAsync(CancellationToken cancellationToken)
        => await _db.BusinessHours.OrderBy(h => h.DayOfWeek).ToListAsync(cancellationToken);
}
