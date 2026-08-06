using Secretary.Application.Abstractions.Persistence;
using Secretary.Domain.Entities;
using Secretary.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Secretary.Infrastructure.Persistence.Repositories;

internal sealed class TenantModuleRepository : ITenantModuleRepository
{
    private readonly AppDbContext _db;

    public TenantModuleRepository(AppDbContext db) => _db = db;

    public async Task<TenantModule?> GetByIdAsync(int id, CancellationToken cancellationToken)
        => await _db.TenantModules.FirstOrDefaultAsync(m => m.Id == id, cancellationToken);

    public async Task<IReadOnlyList<TenantModule>> GetAllAsync(CancellationToken cancellationToken)
        => await _db.TenantModules.ToListAsync(cancellationToken);

    public async Task AddAsync(TenantModule entity, CancellationToken cancellationToken)
        => await _db.TenantModules.AddAsync(entity, cancellationToken);

    public void Update(TenantModule entity) => _db.TenantModules.Update(entity);

    public void Remove(TenantModule entity) => _db.TenantModules.Remove(entity);

    public async Task<IReadOnlyList<TenantModule>> GetForTenantAsync(int tenantId, CancellationToken cancellationToken)
        => await _db.TenantModules
            .Where(m => m.TenantId == tenantId)
            .OrderBy(m => m.Module)
            .ToListAsync(cancellationToken);

    public async Task<TenantModule?> GetAsync(int tenantId, Module module, CancellationToken cancellationToken)
        => await _db.TenantModules.FirstOrDefaultAsync(m => m.TenantId == tenantId && m.Module == module, cancellationToken);

    public async Task<IReadOnlyList<Module>> GetEnabledModulesAsync(int tenantId, CancellationToken cancellationToken)
        => await _db.TenantModules
            .Where(m => m.TenantId == tenantId && m.Status == EntityStatus.Active)
            .OrderBy(m => m.Module)
            .Select(m => m.Module)
            .ToListAsync(cancellationToken);
}
