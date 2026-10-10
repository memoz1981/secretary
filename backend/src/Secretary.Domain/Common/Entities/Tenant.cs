using Secretary.Domain.Abstractions;
using Secretary.Domain.Enums;
using NodaTime;

namespace Secretary.Domain.Entities;

public sealed class Tenant : BaseEntity
{
    public string Name { get; private set; }
    public string Timezone { get; private set; }
    public string? PhoneLine { get; private set; }

    /// <summary>Whether this tenant may see what their AI calls cost to run.
    ///
    /// ⚠ Off for everybody unless the platform admin says otherwise, and it is theirs to set, not
    /// the tenant's. What a call costs us is our commercial position — the margin between it and
    /// what they pay is visible the moment they can read one and know the other. It is switched
    /// on for demonstration tenants during development and is expected to stay off in production.
    ///
    /// The figures are recorded on every call either way. This governs who is shown them, and it
    /// is enforced where the response is built rather than where it is drawn: a number missing
    /// from a page but present in the JSON is not hidden from anybody who can open a browser's
    /// network tab.</summary>
    public bool ShowCallCosts { get; private set; }

    /// <summary>What kind of business this is — a clinic, a car dealer, a water delivery service.
    ///
    /// ⚠ Nullable, and null means nobody has said. The accounts that existed before this column
    /// did are genuinely unknown, and filling them in with a guess would put wrong answers into
    /// the one field whose whole job is segmenting the customer base. The create screen asks for
    /// it, so new accounts arrive with an answer; the old ones read "not set" until somebody who
    /// knows opens them.</summary>
    public BusinessType? BusinessType { get; private set; }

    private Tenant(string name, string timezone, string? phoneLine, BusinessType? businessType, Instant now)
    {
        Name = name;
        Timezone = timezone;
        PhoneLine = phoneLine;
        BusinessType = businessType;
        InitBase(now);
    }

    // EF Core materialization constructor.
    private Tenant()
    {
        Name = string.Empty;
        Timezone = string.Empty;
    }

    public static Tenant Create(
        string name, string timezone, string? phoneLine, bool showCallCosts,
        BusinessType? businessType, Instant now)
    {
        ValidateDetails(name, timezone, businessType);
        return new Tenant(name, timezone, phoneLine, businessType, now) { ShowCallCosts = showCallCosts };
    }

    public void UpdateDetails(
        string name, string timezone, string? phoneLine, bool showCallCosts,
        BusinessType? businessType, Instant now)
    {
        ValidateDetails(name, timezone, businessType);
        Name = name;
        Timezone = timezone;
        PhoneLine = phoneLine;
        ShowCallCosts = showCallCosts;
        BusinessType = businessType;
        Touch(now);
    }

    private static void ValidateDetails(string name, string timezone, BusinessType? businessType)
    {
        // ⚠ A value outside the enum. C# will cast any int to an enum type without complaint, so
        // a client posting businessType: 47 stores 47 and every screen that reads it falls
        // through its lookup to a blank. Refused at the door instead.
        if (businessType is { } type && !Enum.IsDefined(type))
        {
            throw new ArgumentException($"Unknown business type: {(int)type}.", nameof(businessType));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Tenant name is required.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(timezone))
        {
            throw new ArgumentException("Tenant timezone is required.", nameof(timezone));
        }
    }
}
