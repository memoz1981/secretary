using Secretary.Domain.Entities;

namespace Secretary.Application.Abstractions.Persistence;

public interface IServiceOfferingRepository : IRepository<ServiceOffering>
{
    /// <summary>Active service offerings of the current tenant.</summary>
    Task<IReadOnlyList<ServiceOffering>> GetAllForCurrentTenantAsync(CancellationToken cancellationToken);
}
