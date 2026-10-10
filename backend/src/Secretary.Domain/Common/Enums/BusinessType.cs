namespace Secretary.Domain.Enums;

/// <summary>What kind of business a tenant runs.
///
/// ⚠ An enum rather than a table, and the reasoning is worth keeping because the obvious answer
/// is the other one. A table buys you rows an admin can add without a deploy — but this list
/// carries no data of its own, nothing joins to it, and every name has to exist in three
/// languages. In a table that is either one language in a column or three columns and a screen
/// to edit them; here the translations sit beside every other label the app has, where a missing
/// one is a build error rather than a blank cell in production.
///
/// It changes about as often as Module does, and Module is an enum for the same reasons.
///
/// If a type ever needs to carry behaviour — default modules, a starting instruction file — that
/// is a mapping keyed by this enum, which is still not a table.
///
/// Numbered explicitly. The values are stored in the database, so the order here is not free to
/// change and a new type takes the next number rather than a tidy alphabetical position.</summary>
public enum BusinessType
{
    Clinic = 1,
    DentalClinic = 2,
    VetClinic = 3,
    Hospital = 4,
    Pharmacy = 5,
    BeautySalon = 6,
    Barbershop = 7,
    AutoService = 8,
    AutoDealer = 9,
    Shop = 10,
    Restaurant = 11,
    WaterDelivery = 12,
    Courier = 13,

    /// <summary>Parcel forwarding from abroad — CAMEX, Colibri. Not Courier: the work is a
    /// warehouse overseas, customs and a pickup point, and the calls are about where a parcel is,
    /// not when a driver arrives.</summary>
    InternationalCargo = 14,

    /// <summary>A real business that is none of the above. Not the same as unset: this is an
    /// admin who looked at the list and decided nothing fitted, which is worth telling apart
    /// from an admin who never chose.</summary>
    Other = 99,
}
