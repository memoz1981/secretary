using Secretary.Domain.Entities;
using Secretary.Domain.Enums;

namespace Secretary.Application.Abstractions.Persistence;

/// <summary>Every method takes the tenant explicitly. There is no global query filter on this
/// table — the platform admin who grants and revokes has no TenantId, and a filter would hide
/// the rows from the only account allowed to change them.</summary>
public interface ITenantModuleRepository : IRepository<TenantModule>
{
    Task<IReadOnlyList<TenantModule>> GetForTenantAsync(int tenantId, CancellationToken cancellationToken);

    Task<TenantModule?> GetAsync(int tenantId, Module module, CancellationToken cancellationToken);

    /// <summary>Just the enabled ones, which is what a caller almost always wants.</summary>
    Task<IReadOnlyList<Module>> GetEnabledModulesAsync(int tenantId, CancellationToken cancellationToken);
}
