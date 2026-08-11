using Secretary.Domain.Abstractions;
using NodaTime;

namespace Secretary.Domain.Entities;

/// <summary>How a product is counted: ea., kg, m3. Metric, and deliberately tiny.
///
/// Not tenant-scoped — a kilogram is a kilogram — which makes it the one table in the module
/// with no TenantId and no query filter.
///
/// Named MeasurementUnit rather than Unit for the same reason ServiceOffering is not Service:
/// "unit" already means three other things in a layered .NET codebase. The table is Units.
///
/// A bidon is NOT one of these. Sirab's twenty-litre bidon is packaging, not a measure — it is
/// a product counted in ea., and the caller saying "bidon" is matched through the product's
/// aliases. Putting packaging here would mean a Units row per product per container size.</summary>
public sealed class MeasurementUnit : BaseEntity
{
    public string Name { get; private set; }

    private MeasurementUnit(string name, Instant now)
    {
        Name = name;
        InitBase(now);
    }

    private MeasurementUnit()
    {
        Name = string.Empty;
    }

    public static MeasurementUnit Create(string name, Instant now)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Unit name is required.", nameof(name));
        }

        return new MeasurementUnit(name.Trim(), now);
    }

    public void Rename(string name, Instant now)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Unit name is required.", nameof(name));
        }

        Name = name.Trim();
        Touch(now);
    }
}
