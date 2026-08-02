using Secretary.Application.Abstractions.Persistence;
using Secretary.Domain.Entities;
using Secretary.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Secretary.Infrastructure.Persistence.Repositories;

internal sealed class ProviderRepository : IProviderRepository
{
    private readonly AppDbContext _db;

    public ProviderRepository(AppDbContext db) => _db = db;

    public async Task<Provider?> GetByIdAsync(int id, CancellationToken cancellationToken)
        => await _db.Providers.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Provider>> GetAllAsync(CancellationToken cancellationToken)
        => await _db.Providers.ToListAsync(cancellationToken);

    public async Task AddAsync(Provider entity, CancellationToken cancellationToken)
        => await _db.Providers.AddAsync(entity, cancellationToken);

    public void Update(Provider entity) => _db.Providers.Update(entity);

    public void Remove(Provider entity) => _db.Providers.Remove(entity);

    // The DbContext's global tenant query filter already scopes this — only the
    // active/inactive filter is added here.
    public async Task<IReadOnlyList<Provider>> GetAllForCurrentTenantAsync(CancellationToken cancellationToken)
        => await _db.Providers
            .Where(p => p.Status == EntityStatus.Active)
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);
}
