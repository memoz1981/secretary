using Secretary.Domain.Abstractions;
using NodaTime;

namespace Secretary.Domain.Entities;

/// <summary>Something a tenant sells over the phone.
///
/// Aliases are the part that earns its keep. A caller asks for "bidon", "balon" or just "su";
/// the catalogue says "Sirab 20 litrlik bidon". Without somewhere to put the words people
/// actually use, the agent tells a paying customer the business does not sell what it sells —
/// the same failure the appointment module hit with service names, solved the same way.</summary>
public sealed class Product : BaseEntity
{
    public int TenantId { get; private set; }
    public string Name { get; private set; }
    public int MeasurementUnitId { get; private set; }

    /// <summary>Per unit, so the caller hears "üç bidon, altmış manat" rather than a price that
    /// means nothing until they do the arithmetic themselves.</summary>
    public decimal UnitPrice { get; private set; }

    /// <summary>The most of this that one order may contain. Null means no limit.
    ///
    /// Per product because the sane maximum is a property of the thing: twenty bidons of water
    /// is a party, twenty kilos of saffron is a mistake. It exists because a caller was heard as
    /// asking for 255 and the order went through — a misheard number is a lorry-load nobody
    /// ordered, and the cost of finding out is a delivery.</summary>
    public decimal? MaxOrderQuantity { get; private set; }

    /// <summary>Comma-separated words a caller might use for this product. Free text rather than
    /// a table: it is read whole every time and never queried on its own.</summary>
    public string? Aliases { get; private set; }

    private Product(
        int tenantId, string name, int measurementUnitId, decimal unitPrice, string? aliases,
        decimal? maxOrderQuantity, Instant now)
    {
        TenantId = tenantId;
        Name = name;
        MeasurementUnitId = measurementUnitId;
        UnitPrice = unitPrice;
        Aliases = aliases;
        MaxOrderQuantity = maxOrderQuantity;
        InitBase(now);
    }

    private Product()
    {
        Name = string.Empty;
    }

    public static Product Create(
        int tenantId, string name, int measurementUnitId, decimal unitPrice, string? aliases,
        decimal? maxOrderQuantity, Instant now)
    {
        Validate(name, measurementUnitId, unitPrice, maxOrderQuantity);
        return new Product(
            tenantId, name.Trim(), measurementUnitId, unitPrice, Clean(aliases), maxOrderQuantity, now);
    }

    public void UpdateDetails(
        string name, int measurementUnitId, decimal unitPrice, string? aliases, decimal? maxOrderQuantity,
        Instant now)
    {
        Validate(name, measurementUnitId, unitPrice, maxOrderQuantity);
        Name = name.Trim();
        MeasurementUnitId = measurementUnitId;
        UnitPrice = unitPrice;
        Aliases = Clean(aliases);
        MaxOrderQuantity = maxOrderQuantity;
        Touch(now);
    }

    /// <summary>Every word this product answers to, its own name included.</summary>
    public IEnumerable<string> SpokenNames()
    {
        yield return Name;

        if (string.IsNullOrWhiteSpace(Aliases))
        {
            yield break;
        }

        foreach (var alias in Aliases.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            yield return alias;
        }
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void Validate(string name, int measurementUnitId, decimal unitPrice, decimal? maxOrderQuantity)
    {
        if (maxOrderQuantity is <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxOrderQuantity), "A maximum of zero would make the product unorderable; leave it empty for no limit.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Product name is required.", nameof(name));
        }

        if (measurementUnitId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(measurementUnitId), "A product must have a unit.");
        }

        if (unitPrice < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(unitPrice), "Price cannot be negative.");
        }
    }
}
