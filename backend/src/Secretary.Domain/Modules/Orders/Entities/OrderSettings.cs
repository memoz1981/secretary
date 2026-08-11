using Secretary.Domain.Abstractions;
using NodaTime;

namespace Secretary.Domain.Entities;

/// <summary>What a tenant promises an order caller about delivery.
///
/// Sirab says "sabah və ya birigün". That is a sentence a business chooses, not a logistics
/// calculation, so it is a setting rather than a scheduler: how many working days out the agent
/// offers first. The caller can push it later — "birigün olar?" — and the agent counts working
/// days from BusinessHours, so a promise never lands on a day the business is shut.
///
/// One row per tenant, in the module's own schema: a tenant without Orders has no opinion about
/// delivery.</summary>
public sealed class OrderSettings : BaseEntity
{
    /// <summary>Tomorrow. The common promise, and the one that needs no explanation on a call.</summary>
    public const int DefaultLeadWorkingDays = 1;

    public int TenantId { get; private set; }

    /// <summary>Working days from today to the first delivery day offered. 0 is same-day.</summary>
    public int LeadWorkingDays { get; private set; }

    private OrderSettings()
    {
    }

    public static OrderSettings CreateDefault(int tenantId, Instant now)
        => Create(tenantId, DefaultLeadWorkingDays, now);

    public static OrderSettings Create(int tenantId, int leadWorkingDays, Instant now)
    {
        Validate(leadWorkingDays);
        var settings = new OrderSettings { TenantId = tenantId, LeadWorkingDays = leadWorkingDays };
        settings.InitBase(now);
        return settings;
    }

    public void SetLeadWorkingDays(int leadWorkingDays, Instant now)
    {
        Validate(leadWorkingDays);
        LeadWorkingDays = leadWorkingDays;
        Touch(now);
    }

    private static void Validate(int leadWorkingDays)
    {
        if (leadWorkingDays is < 0 or > 14)
        {
            throw new ArgumentOutOfRangeException(
                nameof(leadWorkingDays), "A delivery promise beyond a fortnight is not a promise.");
        }
    }
}
