using Secretary.Domain.Abstractions;
using Secretary.Domain.ValueObjects;
using NodaTime;

namespace Secretary.Domain.Entities;

/// <summary>One of a customer's numbers. Stored normalised, because the same person reads their
/// number out differently every time and a returning caller who is not recognised becomes a
/// second customer record — which is exactly how the Appointment module ended up with two
/// Mehdis.
///
/// A known caller ringing from a new number is the interesting case: identify them first, then
/// add the number. That is what this table is for.</summary>
public sealed class CustomerPhoneNumber : BaseEntity
{
    public int CustomerId { get; private set; }

    /// <summary>E.164 where we recognise the shape — see PhoneNumberNormalizer.</summary>
    public string PhoneNumber { get; private set; }

    /// <summary>The one to call back on. Exactly one per customer, enforced in the service
    /// rather than the schema: a filtered unique index cannot express "at most one true" on
    /// SQL Server and PostgreSQL with the same SQL.</summary>
    public bool IsPrimary { get; private set; }

    private CustomerPhoneNumber(int customerId, string phoneNumber, bool isPrimary, Instant now)
    {
        CustomerId = customerId;
        PhoneNumber = phoneNumber;
        IsPrimary = isPrimary;
        InitBase(now);
    }

    private CustomerPhoneNumber()
    {
        PhoneNumber = string.Empty;
    }

    public static CustomerPhoneNumber Create(int customerId, string phoneNumber, bool isPrimary, Instant now)
    {
        var normalized = PhoneNumberNormalizer.Normalize(phoneNumber);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ArgumentException("A phone number is required.", nameof(phoneNumber));
        }

        return new CustomerPhoneNumber(customerId, normalized, isPrimary, now);
    }

    public void MakePrimary(Instant now)
    {
        IsPrimary = true;
        Touch(now);
    }

    public void MakeSecondary(Instant now)
    {
        IsPrimary = false;
        Touch(now);
    }
}
