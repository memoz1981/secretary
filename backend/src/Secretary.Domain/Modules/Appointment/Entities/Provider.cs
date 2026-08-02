using Secretary.Domain.Abstractions;
using NodaTime;

namespace Secretary.Domain.Entities;

/// <summary>Someone bookable for appointments. Providers have no login of their own —
/// they are not linked to Accounts.</summary>
public sealed class Provider : BaseEntity
{
    public int TenantId { get; private set; }
    public string Name { get; private set; }

    private Provider(int tenantId, string name, Instant now)
    {
        TenantId = tenantId;
        Name = name;
        InitBase(now);
    }

    private Provider()
    {
        Name = string.Empty;
    }

    public static Provider Create(int tenantId, string name, Instant now)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Provider name is required.", nameof(name));
        }

        return new Provider(tenantId, name, now);
    }

    public void UpdateDetails(string name, Instant now)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Provider name is required.", nameof(name));
        }

        Name = name;
        Touch(now);
    }
}
