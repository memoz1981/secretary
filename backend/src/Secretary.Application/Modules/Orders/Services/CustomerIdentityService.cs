using Secretary.Application.Abstractions;
using Secretary.Application.Abstractions.Persistence;
using Secretary.Application.Dtos;
using Secretary.Domain.Entities;
using Secretary.Domain.ValueObjects;
using NodaTime;

namespace Secretary.Application.Services;

/// <summary>Works out who is calling, and says how sure it is.
///
/// Every decision here is deterministic and in code. The model's job is to ask the question this
/// returns and repeat the answer back — never to conclude that it has identified someone. On the
/// appointment line the same model has invented a caller's phone number and confirmed a booking
/// it never made; identity is not something to leave to it.
///
/// Search keys are the customer number and the phone number: unique, and spoken as digits.
/// Name and address are challenge fields — they confirm a match, they never find one. A full
/// address does find a household, but two people live at one address, so it narrows rather than
/// identifies.</summary>
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

    /// <summary>By customer number, or by phone number. Neither identifies on its own.</summary>
    public async Task<CallerIdentityResult> FindAsync(
        int? customerId, string? phoneNumber, CancellationToken cancellationToken)
    {
        if (customerId is > 0)
        {
            var byId = await _uow.Customers.GetByIdAsync(customerId.Value, cancellationToken);
            return byId is null
                ? new CallerIdentityResult(
                    CallerIdentityOutcome.NoSuchCustomer, null, null,
                    "Müştəri nömrənizi rəqəm-rəqəm təkrar edə bilərsiniz?")
                : await ChallengeFor(byId, cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            return new CallerIdentityResult(CallerIdentityOutcome.NotFound, null, null, null);
        }

        var matches = await _uow.Customers.FindByPhoneNumberAsync(
            PhoneNumberNormalizer.Normalize(phoneNumber), cancellationToken);

        return matches.Count switch
        {
            0 => new CallerIdentityResult(CallerIdentityOutcome.NotFound, null, null, null),
            1 => await ChallengeFor(matches[0], cancellationToken),

            // Two people on one number is real — a household, an office. Asking for the name
            // separates them, and it is the one thing they will not have to think about.
            _ => new CallerIdentityResult(
                CallerIdentityOutcome.Ambiguous, null, null, "Adınızı da deyə bilərsiniz?"),
        };
    }

    /// <summary>Checks what the caller said against what is on file.
    ///
    /// The question asked was open — "what is your address?" — never "is your address X?", so
    /// this is a real check rather than a caller agreeing with a prompt. Against a misheard
    /// match, which is the actual failure mode, that is what catches it.</summary>
    public async Task<CallerIdentityResult> ConfirmAsync(
        int customerId, string spokenAnswer, CancellationToken cancellationToken)
    {
        var customer = await _uow.Customers.GetByIdAsync(customerId, cancellationToken);
        if (customer is null)
        {
            return new CallerIdentityResult(CallerIdentityOutcome.NotFound, null, null, null);
        }

        if (string.IsNullOrWhiteSpace(spokenAnswer))
        {
            return await ChallengeFor(customer, cancellationToken);
        }

        var addresses = await _uow.Customers.GetAddressesAsync(customerId, cancellationToken);
        var spokenStreet = AddressText.Normalize(spokenAnswer);

        // Generous on purpose. The caller is reciting their own address down a phone line, and
        // the transcript arrives mangled; requiring every field to line up would reject the
        // right person far more often than it caught the wrong one. A street that appears in
        // what they said is enough, given a customer number or phone number already matched.
        var addressMatches = addresses.Any(a =>
            spokenStreet.Contains(a.StreetNormalized, StringComparison.Ordinal)
            || a.StreetNormalized.Contains(spokenStreet, StringComparison.Ordinal)
            // The rayon on its own counts. It is what they were asked for, there are only twelve
            // of them, and it is the half of the answer a transcript is least likely to mangle.
            || spokenStreet.Contains(AddressText.Normalize(a.District), StringComparison.Ordinal));

        var nameMatches = customer.Name is not null
            && AddressText.Normalize(spokenAnswer).Contains(AddressText.Normalize(customer.Name), StringComparison.Ordinal);

        return addressMatches || nameMatches
            ? new CallerIdentityResult(CallerIdentityOutcome.Identified, customer.Id, customer.Name, null)
            : new CallerIdentityResult(
                CallerIdentityOutcome.NeedsConfirmation, customer.Id, customer.Name,
                "Deyilən ünvan qeydə uyğun gəlmədi. Ünvanı bir daha soruşun.");
    }

    /// <summary>A first call: name, number and address, and the customer number read back.</summary>
    public async Task<Customer> RegisterAsync(NewCustomerDetails details, CancellationToken cancellationToken)
    {
        var tenantId = _currentTenant.TenantId
            ?? throw new InvalidOperationException("This operation requires a tenant-scoped caller.");

        var now = _clock.GetCurrentInstant();
        var customer = Customer.Create(tenantId, details.Name, now);
        await _uow.Customers.AddAsync(customer, cancellationToken);

        // Saved before the children so the identity is assigned — they key on it.
        await _uow.SaveChangesAsync(cancellationToken);

        await _uow.Customers.AddPhoneNumberAsync(
            CustomerPhoneNumber.Create(customer.Id, details.PhoneNumber, isPrimary: true, now), cancellationToken);

        await _uow.Customers.AddAddressAsync(
            CustomerAddress.Create(
                customer.Id, details.District, details.Area, details.Street, details.Lane, details.Building,
                details.Apartment, details.Landmark, details.SpokenAddress, label: null, isDefault: true, now),
            cancellationToken);

        await _uow.SaveChangesAsync(cancellationToken);
        return customer;
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

    /// <summary>Ask for the address, or for the name when there is no address yet. Never states
    /// the stored value: reading it out and inviting "bəli" would confirm nothing, and would
    /// hand a stranger the address of whoever the number really belongs to.</summary>
    private async Task<CallerIdentityResult> ChallengeFor(Customer customer, CancellationToken cancellationToken)
    {
        var addresses = await _uow.Customers.GetAddressesAsync(customer.Id, cancellationToken);

        // The rayon and the street, not the whole address. It is one short phrase rather than a
        // recitation, it is the part the matcher actually compares, and a caller reeling off a
        // building and a flat number gives the transcript more to mangle for no extra
        // certainty.
        var challenge = addresses.Count > 0
            ? "Rayonunuzu və küçənizi deyə bilərsiniz?"
            : "Adınızı deyə bilərsiniz?";

        return new CallerIdentityResult(
            CallerIdentityOutcome.NeedsConfirmation, customer.Id, customer.Name, challenge);
    }
}
