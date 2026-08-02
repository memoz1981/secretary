using Secretary.Domain.Entities;

namespace Secretary.Application.Abstractions.Persistence;

/// <summary>Deliberately not tenant-filtered (see Secretary.Infrastructure's global query
/// filter) — the platform-admin domain operates across all tenants by design.</summary>
public interface ITenantRepository : IRepository<Tenant>
{
    Task<IReadOnlyList<Tenant>> SearchAsync(string? searchText, CancellationToken cancellationToken);
}
