using Secretary.Domain.Entities;

namespace Secretary.Application.Abstractions.Persistence;

public interface IClientRepository : IRepository<Client>
{
    Task<Client?> GetByPhoneNumberAsync(string phoneNumber, CancellationToken cancellationToken);

    /// <summary>Active clients of the current tenant — backs the Clients page.</summary>
    Task<IReadOnlyList<Client>> GetAllForCurrentTenantAsync(CancellationToken cancellationToken);

    /// <summary>Deliberately crosses every tenant, bypassing the usual tenant scoping —
    /// backs the escalation timeout sweep (a system job with no ambient tenant), which needs
    /// the client's phone number for the abandoned-escalation broadcast.</summary>
    Task<Client?> GetByIdAcrossAllTenantsAsync(int id, CancellationToken cancellationToken);
}
