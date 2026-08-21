using Secretary.Domain.Entities;
using Secretary.Domain.Enums;
using NodaTime;

namespace Secretary.Application.Abstractions.Persistence;

public interface ICallRepository : IRepository<Call>
{
    /// <summary>Every call for one tenant, for the platform admin's view of them.
    ///
    /// ⚠ Ignores the tenant query filter and takes the id explicitly. The platform admin has no
    /// TenantId of their own, so the ambient filter — the thing that keeps tenants apart
    /// everywhere else — matches nothing at all for the one caller allowed to look across them.</summary>
    Task<IReadOnlyList<Call>> GetForTenantAsync(int tenantId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Call>> SearchAsync(
        Instant? fromInclusive,
        Instant? toExclusive,
        CallClassification? classification,
        CallOutcome? outcome,
        int? providerId,
        CallPipeline? pipeline,
        CancellationToken cancellationToken);
}
