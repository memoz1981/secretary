using Secretary.Domain.Abstractions;
using NodaTime;

namespace Secretary.Domain.Entities;

/// <summary>A bookable service (name, price, duration). Named "ServiceOffering" rather than
/// "Service" to avoid colliding with the ambient meaning of "service" everywhere else in a
/// layered .NET codebase (DI services, Application services, ...). The base UpdatedAt is
/// bumped on every change and doubles as the AI agent's cache-refresh trigger (Flow G).</summary>
public sealed class ServiceOffering : BaseEntity
{
    public int TenantId { get; private set; }
    public string Name { get; private set; }
    public decimal Price { get; private set; }
    public int DurationMinutes { get; private set; }

    private ServiceOffering(int tenantId, string name, decimal price, int durationMinutes, Instant now)
    {
        TenantId = tenantId;
        Name = name;
        Price = price;
        DurationMinutes = durationMinutes;
        InitBase(now);
    }

    private ServiceOffering()
    {
        Name = string.Empty;
    }

    public static ServiceOffering Create(int tenantId, string name, decimal price, int durationMinutes, Instant now)
    {
        Validate(name, price, durationMinutes);
        return new ServiceOffering(tenantId, name, price, durationMinutes, now);
    }

    public void UpdateDetails(string name, decimal price, int durationMinutes, Instant now)
    {
        Validate(name, price, durationMinutes);
        Name = name;
        Price = price;
        DurationMinutes = durationMinutes;
        Touch(now);
    }

    private static void Validate(string name, decimal price, int durationMinutes)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Service name is required.", nameof(name));
        }

        if (price < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(price), "Price cannot be negative.");
        }

        if (durationMinutes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(durationMinutes), "Duration must be positive.");
        }
    }
}
