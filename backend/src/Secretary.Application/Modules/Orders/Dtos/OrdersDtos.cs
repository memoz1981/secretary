using Secretary.Domain.Entities;
using Secretary.Domain.Enums;
using NodaTime;

namespace Secretary.Application.Dtos;

/// <summary>One customer a lookup turned up, with everywhere we could deliver to them.
///
/// A list of candidates rather than a verdict, which is the opposite of what this used to be.
/// The old shape decided in code whether the caller was identified and handed the model a
/// question to ask; identity is now settled out loud instead — the agent reads back the name and
/// the rayon and the caller agrees or does not. That deletes a tool, a challenge string and a
/// whole branch of the instruction file, and it confirms the same fact.
///
/// ⚠ A match with no address is not returned at all. This is a delivery business: a customer we
/// hold no address for is one we cannot deliver to, so the lookup treats them as not found and
/// registration picks them up — where the phone check reunites them with their existing
/// record rather than making a second one.</summary>
public sealed record CallerMatch(int CustomerId, string? Name, IReadOnlyList<CustomerAddressResponse> Addresses);

/// <summary>Whether registration created a customer or found one already there. The caller reads
/// the number back either way; the flag exists so a returning customer is not told they are
/// new.</summary>
public sealed record RegistrationResult(int CustomerId, bool AlreadyExisted);

public sealed record CustomerAddressResponse(
    int Id, string? Label, string District, string? Area, string Street, string? Lane, string Building,
    string? Apartment, string? Landmark, bool IsDefault)
{
    public static CustomerAddressResponse From(CustomerAddress a) => new(
        a.Id, a.Label, a.District, a.Area, a.Street, a.Lane, a.Building, a.Apartment, a.Landmark, a.IsDefault);

    /// <summary>One line, in the order a Baku address is said.</summary>
    public string Spoken()
    {
        var parts = new List<string> { $"{District} r." };
        if (Area is not null)
        {
            parts.Add(Area);
        }

        parts.Add(Street);
        if (Lane is not null)
        {
            parts.Add(Lane);
        }

        parts.Add($"ev {Building}");
        if (Apartment is not null)
        {
            parts.Add($"mənzil {Apartment}");
        }

        return string.Join(", ", parts);
    }
}

public sealed record ProductResponse(int Id, string Name, string Unit, decimal UnitPrice, decimal? MaxOrderQuantity);

/// <summary>Why a requested delivery day will or will not do.</summary>
public enum DeliveryDayVerdict
{
    Ok = 0,

    /// <summary>The business is shut that weekday.</summary>
    Closed = 1,

    InThePast = 2,

    /// <summary>Sooner than the tenant's lead time. The lead is a floor, not a suggestion — a
    /// business that says ten working days and then delivers tomorrow because the caller asked
    /// has no lead time at all.</summary>
    TooSoon = 4,

    /// <summary>Beyond the window an order may be placed in. Almost always a misheard year — a
    /// caller was offered and accepted the 31st of December 2031.</summary>
    TooFarAhead = 3,
}

public sealed record UnitResponse(int Id, string Name);

public sealed record ProductDetailResponse(
    int Id, string Name, int MeasurementUnitId, string Unit, decimal UnitPrice, string? Aliases,
    decimal? MaxOrderQuantity);

public sealed record CreateProductRequest(
    string Name, int MeasurementUnitId, decimal UnitPrice, string? Aliases, decimal? MaxOrderQuantity);

public sealed record UpdateProductRequest(
    string Name, int MeasurementUnitId, decimal UnitPrice, string? Aliases, decimal? MaxOrderQuantity);

public sealed record OrderLineResponse(string ProductName, decimal Quantity, string Unit, decimal LineTotal);

public sealed record OrderResponse(
    int Id, int CustomerId, string? CustomerName, string DeliveryAddress, string Status,
    Instant PlacedAt, LocalDate? RequestedDeliveryDate, string? Notes,
    IReadOnlyList<OrderLineResponse> Lines, decimal Total);

public sealed record CustomerResponse(
    int Id, string? Name, IReadOnlyList<string> PhoneNumbers, IReadOnlyList<string> Addresses, Instant CreatedAt);

public sealed record SetOrderStatusRequest(OrderStatus Status);

public sealed record OrderSettingsResponse(int LeadWorkingDays, int MaxDeliveryDaysAhead);

public sealed record UpdateOrderSettingsRequest(int LeadWorkingDays, int MaxDeliveryDaysAhead);

/// <summary>One weekday. Both times null means closed — the same one-fact rule the entity
/// keeps, carried out to the wire so the UI cannot send a half-set day.</summary>
public sealed record BusinessHoursDay(IsoDayOfWeek DayOfWeek, LocalTime? OpensAt, LocalTime? ClosesAt);

public sealed record UpdateBusinessHoursRequest(IReadOnlyList<BusinessHoursDay> Days);

/// <summary>What a first call has to collect. Four address fields, not seven: qəsəbə, döngə and
/// landmark were the three heaviest things the agent had to ask for and the three a Baku driver
/// least often needs, and asking for them cost a turn each on every registration. The columns
/// stay on the entity for addresses that already carry them.</summary>
public sealed record NewCustomerDetails(
    string Name, string PhoneNumber, string District, string Street, string Building, string? Apartment);
