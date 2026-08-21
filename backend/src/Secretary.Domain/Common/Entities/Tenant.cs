using Secretary.Domain.Abstractions;
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

    private Tenant(string name, string timezone, string? phoneLine, Instant now)
    {
        Name = name;
        Timezone = timezone;
        PhoneLine = phoneLine;
        InitBase(now);
    }

    // EF Core materialization constructor.
    private Tenant()
    {
        Name = string.Empty;
        Timezone = string.Empty;
    }

    public static Tenant Create(
        string name, string timezone, string? phoneLine, bool showCallCosts, Instant now)
    {
        ValidateDetails(name, timezone);
        return new Tenant(name, timezone, phoneLine, now) { ShowCallCosts = showCallCosts };
    }

    public void UpdateDetails(
        string name, string timezone, string? phoneLine, bool showCallCosts, Instant now)
    {
        ValidateDetails(name, timezone);
        Name = name;
        Timezone = timezone;
        PhoneLine = phoneLine;
        ShowCallCosts = showCallCosts;
        Touch(now);
    }

    private static void ValidateDetails(string name, string timezone)
    {
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
