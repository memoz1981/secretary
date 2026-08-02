using Secretary.Domain.Entities;
using NodaTime;

namespace Secretary.Application.Abstractions.Persistence;

public interface IEscalationRepository : IRepository<Escalation>
{
    Task<IReadOnlyList<Escalation>> GetRingingForCurrentTenantAsync(CancellationToken cancellationToken);

    /// <summary>Deliberately crosses every tenant, bypassing the usual tenant scoping —
    /// backs the ringing-timeout sweep (Flow D's 30s "no one accepted"), which is a system
    /// job with no single ambient tenant, not a per-request call.</summary>
    Task<IReadOnlyList<Escalation>> GetRingingOlderThanAcrossAllTenantsAsync(Instant cutoff, CancellationToken cancellationToken);
}
