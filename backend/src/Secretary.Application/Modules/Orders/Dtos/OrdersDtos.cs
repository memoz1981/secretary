using Secretary.Domain.Entities;
using NodaTime;

namespace Secretary.Application.Dtos;

/// <summary>How sure we are who is calling. A verdict, not a list of candidates — the model
/// must never be the thing that decides an identity, because it will decide it has one.</summary>
public enum CallerIdentityOutcome
{
    /// <summary>Nobody matched. A first call: collect everything.</summary>
    NotFound = 0,

    /// <summary>One customer matched, and the caller has to answer one question before we act on
    /// it. The normal path — a match on a number or a customer number is evidence, not proof,
    /// and the thing it usually gets wrong is a misheard digit rather than a lie.</summary>
    NeedsConfirmation = 1,

    /// <summary>Several customers matched. A shared landline, or two people at one address.</summary>
    Ambiguous = 2,

    /// <summary>Confirmed. Only ConfirmCaller returns this.</summary>
    Identified = 3,

    /// <summary>A customer number was quoted and no such customer exists.
    ///
    /// Not the same as NotFound, and the difference matters more than it looks. Someone who
    /// quotes a number believes they have one, so the likely explanation is that we misheard a
    /// digit — which is exactly what happened: a caller said "two", the model passed 3, and the
    /// agent started registering them as somebody new. Registering is the one response that
    /// cannot be right here.</summary>
    NoSuchCustomer = 4,
}

/// <summary>The verdict plus the single question that resolves it. The question comes from here
/// rather than from the model so it can never be one the answer to is "bəli".</summary>
public sealed record CallerIdentityResult(
    CallerIdentityOutcome Outcome,
    int? CustomerId,
    string? CustomerName,
    string? Challenge);

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

public sealed record OrderSettingsResponse(int LeadWorkingDays, int MaxDeliveryDaysAhead);

public sealed record UpdateOrderSettingsRequest(int LeadWorkingDays, int MaxDeliveryDaysAhead);

/// <summary>One weekday. Both times null means closed — the same one-fact rule the entity
/// keeps, carried out to the wire so the UI cannot send a half-set day.</summary>
public sealed record BusinessHoursDay(IsoDayOfWeek DayOfWeek, LocalTime? OpensAt, LocalTime? ClosesAt);

public sealed record UpdateBusinessHoursRequest(IReadOnlyList<BusinessHoursDay> Days);

public sealed record NewCustomerDetails(
    string Name, string PhoneNumber, string District, string? Area, string Street, string? Lane, string Building,
    string? Apartment, string? Landmark, string? SpokenAddress);
