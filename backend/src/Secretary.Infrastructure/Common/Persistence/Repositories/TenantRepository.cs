using Secretary.Application.Abstractions.Persistence;
using Secretary.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Secretary.Infrastructure.Persistence.Repositories;

internal sealed class TenantRepository : ITenantRepository
{
    private readonly AppDbContext _db;

    public TenantRepository(AppDbContext db) => _db = db;

    public async Task<Tenant?> GetByIdAsync(int id, CancellationToken cancellationToken)
        => await _db.Tenants.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Tenant>> GetAllAsync(CancellationToken cancellationToken)
        => await _db.Tenants.ToListAsync(cancellationToken);

    public async Task AddAsync(Tenant entity, CancellationToken cancellationToken)
        => await _db.Tenants.AddAsync(entity, cancellationToken);

    public void Update(Tenant entity) => _db.Tenants.Update(entity);

    public void Remove(Tenant entity) => _db.Tenants.Remove(entity);

    public async Task<IReadOnlyList<Tenant>> SearchAsync(string? searchText, CancellationToken cancellationToken)
    {
        var query = _db.Tenants.AsQueryable();
        if (!string.IsNullOrWhiteSpace(searchText))
        {
            query = query.Where(t => EF.Functions.Like(t.Name, $"%{searchText}%"));
        }

        return await query.ToListAsync(cancellationToken);
    }
}
