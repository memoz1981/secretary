using Secretary.Application.Abstractions.Persistence;
using Secretary.Domain.Entities;
using Secretary.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Secretary.Infrastructure.Persistence.Repositories;

internal sealed class ProviderServiceOfferingRepository : IProviderServiceOfferingRepository
{
    private readonly AppDbContext _db;

    public ProviderServiceOfferingRepository(AppDbContext db) => _db = db;

    public async Task<ProviderServiceOffering?> GetByIdAsync(int id, CancellationToken cancellationToken)
        => await _db.ProviderServiceOfferings.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task<IReadOnlyList<ProviderServiceOffering>> GetAllAsync(CancellationToken cancellationToken)
        => await _db.ProviderServiceOfferings.ToListAsync(cancellationToken);

    public async Task AddAsync(ProviderServiceOffering entity, CancellationToken cancellationToken)
        => await _db.ProviderServiceOfferings.AddAsync(entity, cancellationToken);

    public void Update(ProviderServiceOffering entity) => _db.ProviderServiceOfferings.Update(entity);

    public void Remove(ProviderServiceOffering entity) => _db.ProviderServiceOfferings.Remove(entity);

    // The DbContext's global tenant query filter already scopes these.
    public async Task<IReadOnlyList<ProviderServiceOffering>> GetAllForCurrentTenantAsync(CancellationToken cancellationToken)
        => await _db.ProviderServiceOfferings.ToListAsync(cancellationToken);

    public async Task<ProviderServiceOffering?> GetByProviderAndOfferingAsync(
        int providerId, int serviceOfferingId, CancellationToken cancellationToken)
        => await _db.ProviderServiceOfferings.FirstOrDefaultAsync(
            p => p.ProviderId == providerId && p.ServiceOfferingId == serviceOfferingId, cancellationToken);

    public async Task<IReadOnlyList<int>> GetActiveProviderIdsForOfferingAsync(
        int serviceOfferingId, CancellationToken cancellationToken)
        => await _db.ProviderServiceOfferings
            .Where(p => p.ServiceOfferingId == serviceOfferingId && p.Status == EntityStatus.Active)
            .Select(p => p.ProviderId)
            .ToListAsync(cancellationToken);
}
