using Secretary.Domain.Entities;

namespace Secretary.Application.Abstractions.Persistence;

/// <summary>The provider×service-offering correspondence (see the entity's own note: rows
/// are never deleted; Status Inactive means "provider doesn't offer this service").</summary>
public interface IProviderServiceOfferingRepository : IRepository<ProviderServiceOffering>
{
    /// <summary>Every assignment row of the current tenant, active and inactive — backs the
    /// Providers page checkbox matrix.</summary>
    Task<IReadOnlyList<ProviderServiceOffering>> GetAllForCurrentTenantAsync(CancellationToken cancellationToken);

    Task<ProviderServiceOffering?> GetByProviderAndOfferingAsync(
        int providerId, int serviceOfferingId, CancellationToken cancellationToken);

    /// <summary>Ids of providers that actively offer the given service — the agent must only
    /// propose these for availability/booking.</summary>
    Task<IReadOnlyList<int>> GetActiveProviderIdsForOfferingAsync(int serviceOfferingId, CancellationToken cancellationToken);
}
