using Secretary.Application.Abstractions;
using Secretary.Application.Abstractions.Persistence;
using Secretary.Application.Dtos;
using Secretary.Domain.Entities;
using Secretary.Domain.ValueObjects;
using NodaTime;

namespace Secretary.Application.Services;

/// <summary>Works out who is calling — three ways in, one shape out.
///
/// Each lookup returns the customers it found together with their addresses, and nothing here
/// decides that the caller is who they say. The check happens out loud: the agent reads back the
/// name and the rayon, and the caller agrees or does not. That replaced a confirm tool and a
/// generated challenge question, and it catches the same thing they did — a misheard digit —
/// without a second round trip.
///
/// Search keys are the customer number, the phone number, and the rayon plus street. The first
/// two are unique and spoken as digits. The third finds a household rather than a person, which
/// is why a name still has to come back correct.</summary>
public sealed class CustomerIdentityService
{
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;
    private readonly ICurrentTenantProvider _currentTenant;

    public CustomerIdentityService(IUnitOfWork uow, IClock clock, ICurrentTenantProvider currentTenant)
    {
        _uow = uow;
        _clock = clock;
        _currentTenant = currentTenant;
    }

    /// <summary>By customer number. At most one, and no challenge — a caller who quotes their
    /// number has produced the one piece of evidence nobody else has, and being asked for their
    /// street straight afterwards reads as not being believed. The number is not secret, so this
    /// is a real trade: for water on cash delivery it is worth the turn it saves on every call,
    /// and it would not be for anything valuable.</summary>
    public async Task<IReadOnlyList<CallerMatch>> FindByIdAsync(int customerId, CancellationToken cancellationToken)
    {
        if (customerId <= 0)
        {
            return [];
        }

        var customer = await _uow.Customers.GetByIdAsync(customerId, cancellationToken);
        return customer is null ? [] : await DescribeAsync([customer], cancellationToken);
    }

    /// <summary>By phone number. More than one is ordinary — a household landline, an office —
    /// so this can hand back several and the agent asks which name.</summary>
    public async Task<IReadOnlyList<CallerMatch>> FindByPhoneAsync(
        string phoneNumber, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            return [];
        }

        var matches = await _uow.Customers.FindByPhoneNumberAsync(
            PhoneNumberNormalizer.Normalize(phoneNumber), cancellationToken);

        return await DescribeAsync(matches, cancellationToken);
    }

    /// <summary>By where they live. The rayon and the street arrive as separate fields rather
    /// than one spoken line, because the agent has to ask for them separately anyway and slicing
    /// a recited address apart afterwards is where it went wrong.</summary>
    public async Task<IReadOnlyList<CallerMatch>> FindByAddressAsync(
        string district, string street, CancellationToken cancellationToken)
    {
        var canonicalDistrict = BakuDistricts.Match(district);
        var normalizedStreet = AddressText.Normalize(street);
        if (canonicalDistrict is null || normalizedStreet.Length == 0)
        {
            return [];
        }

        // One query. This used to read every customer and then their addresses one at a time,
        // which is fine at two customers and a table scan per call at two thousand.
        var matches = await _uow.Customers.FindByAddressAsync(
            canonicalDistrict, normalizedStreet, cancellationToken);

        return await DescribeAsync(matches, cancellationToken);
    }

    /// <summary>A first call: name, number and address, and the customer number read back.
    ///
    /// ⚠ Checks the phone number before inserting. Not finding a customer now sends the caller
    /// here rather than ending the call, and a misheard digit — 050 for 055 — would otherwise
    /// make a second Elvin at the same address that nobody notices until the driver does.</summary>
    public async Task<RegistrationResult> RegisterAsync(
        NewCustomerDetails details, CancellationToken cancellationToken)
    {
        var tenantId = _currentTenant.TenantId
            ?? throw new InvalidOperationException("This operation requires a tenant-scoped caller.");

        var now = _clock.GetCurrentInstant();
        var existing = (await _uow.Customers.FindByPhoneNumberAsync(
            PhoneNumberNormalizer.Normalize(details.PhoneNumber), cancellationToken)).FirstOrDefault();

        if (existing is not null)
        {
            // They are already ours — the lookup missed them, most likely because the number was
            // heard differently or they had no address on file. Give them the address rather
            // than a second identity.
            await AddAddressIfNewAsync(existing.Id, details, now, cancellationToken);
            await _uow.SaveChangesAsync(cancellationToken);
            return new RegistrationResult(existing.Id, AlreadyExisted: true);
        }

        var customer = Customer.Create(tenantId, details.Name, now);
        await _uow.Customers.AddAsync(customer, cancellationToken);

        // Saved before the children so the identity is assigned — they key on it.
        await _uow.SaveChangesAsync(cancellationToken);

        await _uow.Customers.AddPhoneNumberAsync(
            CustomerPhoneNumber.Create(customer.Id, details.PhoneNumber, isPrimary: true, now), cancellationToken);

        await AddAddressIfNewAsync(customer.Id, details, now, cancellationToken);

        await _uow.SaveChangesAsync(cancellationToken);
        return new RegistrationResult(customer.Id, AlreadyExisted: false);
    }

    /// <summary>Adds a number to a customer we have already identified. The reason the numbers
    /// are a table: a known caller ringing from a new phone should not become a second
    /// record.</summary>
    public async Task AddPhoneNumberAsync(int customerId, string phoneNumber, CancellationToken cancellationToken)
    {
        var existing = await _uow.Customers.GetPhoneNumbersAsync(customerId, cancellationToken);
        var normalized = PhoneNumberNormalizer.Normalize(phoneNumber);
        if (existing.Any(p => p.PhoneNumber == normalized))
        {
            return;
        }

        await _uow.Customers.AddPhoneNumberAsync(
            CustomerPhoneNumber.Create(customerId, phoneNumber, isPrimary: false, _clock.GetCurrentInstant()),
            cancellationToken);

        await _uow.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<string>> GetPhoneNumbersAsync(int customerId, CancellationToken cancellationToken)
    {
        var numbers = await _uow.Customers.GetPhoneNumbersAsync(customerId, cancellationToken);
        return numbers.Select(p => p.PhoneNumber).ToList();
    }

    public async Task<IReadOnlyList<CustomerAddressResponse>> GetAddressesAsync(
        int customerId, CancellationToken cancellationToken)
    {
        var addresses = await _uow.Customers.GetAddressesAsync(customerId, cancellationToken);
        return addresses.Select(CustomerAddressResponse.From).ToList();
    }

    /// <summary>Attaches the addresses, and drops anyone we hold none for — see CallerMatch.</summary>
    private async Task<IReadOnlyList<CallerMatch>> DescribeAsync(
        IReadOnlyList<Customer> customers, CancellationToken cancellationToken)
    {
        var described = new List<CallerMatch>(customers.Count);
        foreach (var customer in customers)
        {
            var addresses = await _uow.Customers.GetAddressesAsync(customer.Id, cancellationToken);
            if (addresses.Count == 0)
            {
                continue;
            }

            described.Add(new CallerMatch(
                customer.Id, customer.Name, addresses.Select(CustomerAddressResponse.From).ToList()));
        }

        return described;
    }

    private async Task AddAddressIfNewAsync(
        int customerId, NewCustomerDetails details, Instant now, CancellationToken cancellationToken)
    {
        var existing = await _uow.Customers.GetAddressesAsync(customerId, cancellationToken);
        var canonicalDistrict = BakuDistricts.Match(details.District)
            ?? throw new ArgumentException($"'{details.District}' is not a Baku rayon.", nameof(details));

        var street = AddressText.Normalize(details.Street);
        var building = AddressText.Normalize(details.Building);
        if (existing.Any(a =>
                a.District == canonicalDistrict && a.StreetNormalized == street && a.BuildingNormalized == building))
        {
            return;
        }

        await _uow.Customers.AddAddressAsync(
            CustomerAddress.Create(
                customerId, details.District, area: null, details.Street, lane: null, details.Building,
                details.Apartment, landmark: null, spokenText: null, label: null,
                isDefault: existing.Count == 0, now),
            cancellationToken);
    }
}
