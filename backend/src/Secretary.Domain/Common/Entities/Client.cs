using Secretary.Domain.Abstractions;
using NodaTime;

namespace Secretary.Domain.Entities;

/// <summary>A caller, identified by phone number — the AI agent's primary lookup key
/// (Stage 1). Name is learned over the course of calls, not required upfront. A blacklisted
/// client can still be looked up, but the agent must refuse to book for them.</summary>
public sealed class Client : BaseEntity
{
    public int TenantId { get; private set; }
    public string PhoneNumber { get; private set; }
    public string? Name { get; private set; }
    public bool BlackListed { get; private set; }
    public string? BlackListReason { get; private set; }

    private Client(int tenantId, string phoneNumber, string? name, Instant now)
    {
        TenantId = tenantId;
        PhoneNumber = phoneNumber;
        Name = name;
        InitBase(now);
    }

    private Client()
    {
        PhoneNumber = string.Empty;
    }

    public static Client Create(int tenantId, string phoneNumber, Instant now, string? name = null)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            throw new ArgumentException("Phone number is required.", nameof(phoneNumber));
        }

        return new Client(tenantId, phoneNumber, name, now);
    }

    public void UpdateName(string name, Instant now)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name cannot be blank.", nameof(name));
        }

        Name = name;
        Touch(now);
    }

    public void UpdateDetails(string phoneNumber, string? name, Instant now)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            throw new ArgumentException("Phone number is required.", nameof(phoneNumber));
        }

        PhoneNumber = phoneNumber;
        Name = string.IsNullOrWhiteSpace(name) ? null : name;
        Touch(now);
    }

    public void BlackList(string? reason, Instant now)
    {
        BlackListed = true;
        BlackListReason = string.IsNullOrWhiteSpace(reason) ? null : reason;
        Touch(now);
    }

    public void UndoBlackList(Instant now)
    {
        BlackListed = false;
        BlackListReason = null;
        Touch(now);
    }
}
