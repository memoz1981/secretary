using Secretary.Application.Abstractions.Persistence;
using Secretary.Domain.Entities;
using Secretary.Domain.Enums;
using Secretary.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Secretary.Infrastructure.Persistence.Repositories;

internal sealed class ClientRepository : IClientRepository
{
    private readonly AppDbContext _db;

    public ClientRepository(AppDbContext db) => _db = db;

    public async Task<Client?> GetByIdAsync(int id, CancellationToken cancellationToken)
        => await _db.Clients.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Client>> GetAllAsync(CancellationToken cancellationToken)
        => await _db.Clients.ToListAsync(cancellationToken);

    public async Task AddAsync(Client entity, CancellationToken cancellationToken)
        => await _db.Clients.AddAsync(entity, cancellationToken);

    public void Update(Client entity) => _db.Clients.Update(entity);

    public void Remove(Client entity) => _db.Clients.Remove(entity);

    /// <summary>Normalised here rather than at each call site, so every lookup matches the
    /// canonical form Client stores — a caller who reads their number out differently on a
    /// second call is still found. See PhoneNumberNormalizer.</summary>
    public async Task<Client?> GetByPhoneNumberAsync(string phoneNumber, CancellationToken cancellationToken)
    {
        var normalized = PhoneNumberNormalizer.Normalize(phoneNumber);
        return await _db.Clients.FirstOrDefaultAsync(c => c.PhoneNumber == normalized, cancellationToken);
    }

    public async Task<Client?> GetByIdAcrossAllTenantsAsync(int id, CancellationToken cancellationToken)
        => await _db.Clients.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    // The DbContext's global tenant query filter already scopes this — only the
    // active/inactive filter is added here.
    public async Task<IReadOnlyList<Client>> GetAllForCurrentTenantAsync(CancellationToken cancellationToken)
        => await _db.Clients
            .Where(c => c.Status == EntityStatus.Active)
            .OrderBy(c => c.Name).ThenBy(c => c.PhoneNumber)
            .ToListAsync(cancellationToken);
}
