using Secretary.Domain.Abstractions;
using NodaTime;

namespace Secretary.Domain.Entities;

/// <summary>Someone who orders. Their Id is the customer number they are read at the end of a
/// first call and quote on every one after — the surrogate key doing double duty, so there is
/// no second number to keep in step, and it is never reused.
///
/// Called Customer, not Client, for two reasons. The flat namespaces mean there can only be one
/// Client in Secretary.Domain.Entities and the Appointment module has it — but the better reason
/// is that they are genuinely different people. A caller booking a haircut and a caller ordering
/// water are two records in two schemas, which is the whole point of the module split.
///
/// Phone numbers and addresses are one-to-many both ways round: a household has a mobile and a
/// landline, and a person orders to home and to work. Two people at one address are simply two
/// customers with two address rows — no shared address entity, because deduplicating addresses
/// buys nothing and costs a join.</summary>
public sealed class Customer : BaseEntity
{
    public int TenantId { get; private set; }
    public string? Name { get; private set; }

    private Customer(int tenantId, string? name, Instant now)
    {
        TenantId = tenantId;
        Name = name;
        InitBase(now);
    }

    private Customer()
    {
    }

    public static Customer Create(int tenantId, string? name, Instant now)
        => new(tenantId, string.IsNullOrWhiteSpace(name) ? null : name.Trim(), now);

    public void Rename(string? name, Instant now)
    {
        Name = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
        Touch(now);
    }
}
