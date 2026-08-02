using Secretary.Application.Abstractions.Persistence;
using Secretary.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Secretary.Infrastructure.Persistence.Repositories;

internal sealed class AccountRepository : IAccountRepository
{
    private readonly AppDbContext _db;

    public AccountRepository(AppDbContext db) => _db = db;

    public async Task<Account?> GetByIdAsync(int id, CancellationToken cancellationToken)
        => await _db.Accounts.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Account>> GetAllAsync(CancellationToken cancellationToken)
        => await _db.Accounts.ToListAsync(cancellationToken);

    public async Task AddAsync(Account entity, CancellationToken cancellationToken)
        => await _db.Accounts.AddAsync(entity, cancellationToken);

    public void Update(Account entity) => _db.Accounts.Update(entity);

    public void Remove(Account entity) => _db.Accounts.Remove(entity);

    public async Task<Account?> GetByEmailAsync(string email, CancellationToken cancellationToken)
        => await _db.Accounts.FirstOrDefaultAsync(a => a.Email == email, cancellationToken);

    public async Task<IReadOnlyList<Account>> GetByTenantAsync(int tenantId, CancellationToken cancellationToken)
        => await _db.Accounts.Where(a => a.TenantId == tenantId).ToListAsync(cancellationToken);
}
