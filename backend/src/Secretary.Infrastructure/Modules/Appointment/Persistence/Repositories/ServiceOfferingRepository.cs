using Secretary.Application.Abstractions.Persistence;
using Secretary.Domain.Entities;
using Secretary.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Secretary.Infrastructure.Persistence.Repositories;

internal sealed class ServiceOfferingRepository : IServiceOfferingRepository
{
    private readonly AppDbContext _db;

    public ServiceOfferingRepository(AppDbContext db) => _db = db;

    public async Task<ServiceOffering?> GetByIdAsync(int id, CancellationToken cancellationToken)
        => await _db.ServiceOfferings.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public async Task<IReadOnlyList<ServiceOffering>> GetAllAsync(CancellationToken cancellationToken)
        => await _db.ServiceOfferings.ToListAsync(cancellationToken);

    public async Task AddAsync(ServiceOffering entity, CancellationToken cancellationToken)
        => await _db.ServiceOfferings.AddAsync(entity, cancellationToken);

    public void Update(ServiceOffering entity) => _db.ServiceOfferings.Update(entity);

    public void Remove(ServiceOffering entity) => _db.ServiceOfferings.Remove(entity);

    public async Task<IReadOnlyList<ServiceOffering>> GetAllForCurrentTenantAsync(CancellationToken cancellationToken)
        => await _db.ServiceOfferings
            .Where(s => s.Status == EntityStatus.Active)
            .OrderBy(s => s.Name)
            .ToListAsync(cancellationToken);
}
