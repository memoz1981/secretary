using Secretary.Domain.Entities;

namespace Secretary.Application.Abstractions.Persistence;

public interface IProviderRepository : IRepository<Provider>
{
    /// <summary>Active providers of the current tenant.</summary>
    Task<IReadOnlyList<Provider>> GetAllForCurrentTenantAsync(CancellationToken cancellationToken);
}
