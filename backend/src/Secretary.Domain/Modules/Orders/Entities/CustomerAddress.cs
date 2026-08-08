using Secretary.Domain.Abstractions;
using Secretary.Domain.ValueObjects;
using NodaTime;

namespace Secretary.Domain.Entities;

/// <summary>Where an order goes, in the shape Baku addresses actually take.
///
/// Structured enough to compare, loose enough to survive speech. District is one of twelve
/// rayons; Area is the qəsəbə or massiv, which private-house addresses lean on more than the
/// rayon; Lane is the döngə, without which house 12 is ambiguous on every street that has one.
/// Building is text because 12A and 12/3 are ordinary. Apartment absent means a private house —
/// a better signal than asking the caller what kind of building they live in.
///
/// SpokenText is the whole of what the caller said, kept verbatim. Speech does not arrive
/// pre-slotted, the model fills these fields and the model invents things; when the slots come
/// out wrong, this is what a human reads. It is also the only honest way to improve the parsing
/// later.
///
/// The normalised columns exist for one job: deciding whether the address a caller just recited
/// is the one on file. Never shown, never edited.</summary>
public sealed class CustomerAddress : BaseEntity
{
    public int CustomerId { get; private set; }

    /// <summary>What the caller calls this address — "ev", "iş". Optional.</summary>
    public string? Label { get; private set; }

    public string District { get; private set; }
    public string? Area { get; private set; }
    public string Street { get; private set; }
    public string? Lane { get; private set; }

    /// <summary>The döngə as a number, where it is one. Null for a named lane.</summary>
    public int? LaneNumber { get; private set; }

    public string Building { get; private set; }
    public string? Apartment { get; private set; }
    public string? Landmark { get; private set; }
    public string? SpokenText { get; private set; }

    /// <summary>Where an order goes when the customer does not say. The first address saved.</summary>
    public bool IsDefault { get; private set; }

    public string StreetNormalized { get; private set; }
    public string BuildingNormalized { get; private set; }

    private CustomerAddress()
    {
        District = string.Empty;
        Street = string.Empty;
        Building = string.Empty;
        StreetNormalized = string.Empty;
        BuildingNormalized = string.Empty;
    }

    public static CustomerAddress Create(
        int customerId, string district, string? area, string street, string? lane, string building,
        string? apartment, string? landmark, string? spokenText, string? label, bool isDefault, Instant now)
    {
        var canonicalDistrict = BakuDistricts.Match(district)
            ?? throw new ArgumentException($"'{district}' is not a Baku rayon.", nameof(district));

        if (string.IsNullOrWhiteSpace(street))
        {
            throw new ArgumentException("Street is required.", nameof(street));
        }

        if (string.IsNullOrWhiteSpace(building))
        {
            throw new ArgumentException("Building is required.", nameof(building));
        }

        var address = new CustomerAddress
        {
            CustomerId = customerId,
            District = canonicalDistrict,
            Area = Clean(area),
            Street = street.Trim(),
            Lane = Clean(lane),
            LaneNumber = ValueObjects.LaneNumber.Parse(lane),
            Building = building.Trim(),
            Apartment = Clean(apartment),
            Landmark = Clean(landmark),
            SpokenText = Clean(spokenText),
            Label = Clean(label),
            IsDefault = isDefault,
            StreetNormalized = AddressText.Normalize(street),
            BuildingNormalized = AddressText.Normalize(building),
        };

        address.InitBase(now);
        return address;
    }

    public void MakeDefault(Instant now)
    {
        IsDefault = true;
        Touch(now);
    }

    public void MakeSecondary(Instant now)
    {
        IsDefault = false;
        Touch(now);
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
