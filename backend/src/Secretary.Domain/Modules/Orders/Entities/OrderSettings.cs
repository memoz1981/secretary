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

    /// <summary>A month. Far enough for anyone genuinely planning ahead, near enough that a
    /// misheard year is caught — a caller was offered and accepted the 31st of December 2031,
    /// which is a Wednesday and so passed the only check there was.</summary>
    public const int DefaultMaxDeliveryDaysAhead = 30;

    public int TenantId { get; private set; }

    /// <summary>Working days from today to the first delivery day offered. 0 is same-day.</summary>
    public int LeadWorkingDays { get; private set; }

    /// <summary>The furthest ahead an order may be placed, in calendar days. Tenant-wide rather
    /// than per product: it is a limit on how far this business will commit, not a property of
    /// anything on the shelf.</summary>
    public int MaxDeliveryDaysAhead { get; private set; }

    private OrderSettings()
    {
    }

    public static OrderSettings CreateDefault(int tenantId, Instant now)
        => Create(tenantId, DefaultLeadWorkingDays, DefaultMaxDeliveryDaysAhead, now);

    public static OrderSettings Create(int tenantId, int leadWorkingDays, int maxDeliveryDaysAhead, Instant now)
    {
        Validate(leadWorkingDays, maxDeliveryDaysAhead);
        var settings = new OrderSettings
        {
            TenantId = tenantId,
            LeadWorkingDays = leadWorkingDays,
            MaxDeliveryDaysAhead = maxDeliveryDaysAhead,
        };

        settings.InitBase(now);
        return settings;
    }

    public void Update(int leadWorkingDays, int maxDeliveryDaysAhead, Instant now)
    {
        Validate(leadWorkingDays, maxDeliveryDaysAhead);
        LeadWorkingDays = leadWorkingDays;
        MaxDeliveryDaysAhead = maxDeliveryDaysAhead;
        Touch(now);
    }

    private static void Validate(int leadWorkingDays, int maxDeliveryDaysAhead)
    {
        if (leadWorkingDays is < 0 or > 14)
        {
            throw new ArgumentOutOfRangeException(
                nameof(leadWorkingDays), "A delivery promise beyond a fortnight is not a promise.");
        }

        if (maxDeliveryDaysAhead is < 1 or > 365)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxDeliveryDaysAhead), "The furthest delivery day must be between a day and a year away.");
        }

        // A window that ends before the promise starts would refuse every day including the one
        // the agent offers first.
        if (maxDeliveryDaysAhead < leadWorkingDays)
        {
            throw new ArgumentException(
                "The furthest delivery day cannot be sooner than the first one offered.", nameof(maxDeliveryDaysAhead));
        }
    }
}
