using Secretary.Domain.Abstractions;
using NodaTime;

namespace Secretary.Domain.Entities;

public sealed class Tenant : BaseEntity
{
    public string Name { get; private set; }
    public string Timezone { get; private set; }
    public string? PhoneLine { get; private set; }

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

    public static Tenant Create(string name, string timezone, string? phoneLine, Instant now)
    {
        ValidateDetails(name, timezone);
        return new Tenant(name, timezone, phoneLine, now);
    }

    public void UpdateDetails(string name, string timezone, string? phoneLine, Instant now)
    {
        ValidateDetails(name, timezone);
        Name = name;
        Timezone = timezone;
        PhoneLine = phoneLine;
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
