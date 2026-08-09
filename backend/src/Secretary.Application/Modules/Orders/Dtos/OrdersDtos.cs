using Secretary.Domain.Entities;

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

public sealed record ProductResponse(int Id, string Name, string? Description, string Unit, decimal UnitPrice);

public sealed record NewCustomerDetails(
    string Name, string PhoneNumber, string District, string? Area, string Street, string? Lane, string Building,
    string? Apartment, string? Landmark, string? SpokenAddress);
