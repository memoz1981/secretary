using Secretary.Domain.Entities;

namespace Secretary.Application.Abstractions.Persistence;

public interface IAccountRepository : IRepository<Account>
{
    /// <summary>Looks up across all tenants — used only by login, before the caller's
    /// tenant is known.</summary>
    Task<Account?> GetByEmailAsync(string email, CancellationToken cancellationToken);

    Task<IReadOnlyList<Account>> GetByTenantAsync(int tenantId, CancellationToken cancellationToken);
}
