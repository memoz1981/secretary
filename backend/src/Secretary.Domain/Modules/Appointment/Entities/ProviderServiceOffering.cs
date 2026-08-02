using Secretary.Domain.Abstractions;
using NodaTime;

namespace Secretary.Domain.Entities;

/// <summary>Which providers perform which service offerings. A row is created for every
/// provider×offering pair (new offerings are assigned to all providers, new providers get
/// all offerings) and the tenant unchecks pairs via Status — Inactive means "does not
/// provide this service". Rows are never deleted, so the assignment history is auditable.</summary>
public sealed class ProviderServiceOffering : BaseEntity
{
    public int TenantId { get; private set; }
    public int ProviderId { get; private set; }
    public int ServiceOfferingId { get; private set; }

    private ProviderServiceOffering(int tenantId, int providerId, int serviceOfferingId, Instant now)
    {
        TenantId = tenantId;
        ProviderId = providerId;
        ServiceOfferingId = serviceOfferingId;
        InitBase(now);
    }

    private ProviderServiceOffering()
    {
    }

    public static ProviderServiceOffering Create(int tenantId, int providerId, int serviceOfferingId, Instant now)
        => new(tenantId, providerId, serviceOfferingId, now);
}
