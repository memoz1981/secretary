using Secretary.Application.Abstractions;
using Secretary.Application.Abstractions.Persistence;
using Secretary.Application.Dtos;
using Secretary.Domain.Entities;
using Secretary.Domain.ValueObjects;
using NodaTime;

namespace Secretary.Application.Services;

/// <summary>The catalogue, the delivery promise, and placing the order.</summary>
public sealed class OrderService
{
    private readonly IUnitOfWork _uow;
    private readonly BusinessHoursService _businessHours;
    private readonly IClock _clock;
    private readonly ICurrentTenantProvider _currentTenant;

    public OrderService(
        IUnitOfWork uow, BusinessHoursService businessHours, IClock clock, ICurrentTenantProvider currentTenant)
    {
        _uow = uow;
        _businessHours = businessHours;
        _clock = clock;
        _currentTenant = currentTenant;
    }

    public async Task<IReadOnlyList<ProductResponse>> GetCatalogAsync(CancellationToken cancellationToken)
    {
        var products = await _uow.Products.GetCatalogAsync(cancellationToken);
        var units = await _uow.Units.GetAllAsync(cancellationToken);
        var unitNames = units.ToDictionary(u => u.Id, u => u.Name);

        return products
            .Select(p => new ProductResponse(
                p.Id, p.Name, p.Description, unitNames.GetValueOrDefault(p.MeasurementUnitId, "ea."), p.UnitPrice))
            .ToList();
    }

    /// <summary>What the caller asked for, matched against the catalogue including the words
    /// people actually use for it. A caller says "bidon"; the catalogue says "Sirab 20 litrlik
    /// bidon".</summary>
    public async Task<Product?> MatchProductAsync(string spokenName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(spokenName))
        {
            return null;
        }

        var catalog = await _uow.Products.GetCatalogAsync(cancellationToken);

        // Folded, not compared raw. A caller said "Şirab" — correctly — and the catalogue spells
        // it "Sirab" with a Latin S, so an OrdinalIgnoreCase comparison said the business does
        // not sell it. Azerbaijani names will differ by ş/s, ə/e, ç/c, ğ/g, ı/i, ö/o or ü/u from
        // however anyone typed them in, every time. AddressText.Normalize already folds exactly
        // these, and it strips street words that no product name contains.
        var needle = AddressText.Normalize(spokenName);
        if (needle.Length == 0)
        {
            return null;
        }

        return catalog.FirstOrDefault(p => p.SpokenNames().Any(n => AddressText.Normalize(n) == needle))
            ?? catalog.FirstOrDefault(p => p.SpokenNames().Any(n =>
            {
                var folded = AddressText.Normalize(n);
                return folded.Length > 0
                    && (folded.Contains(needle, StringComparison.Ordinal)
                        || needle.Contains(folded, StringComparison.Ordinal));
            }));
    }

    /// <summary>The first day the tenant will promise, counting working days so that "tomorrow"
    /// on a Saturday does not promise a closed Sunday. Null when the business is shut for the
    /// next fortnight, which is a configuration problem the agent should escalate rather than
    /// invent an answer for.</summary>
    public async Task<LocalDate?> ProposeDeliveryDayAsync(CancellationToken cancellationToken)
    {
        var settings = await _uow.OrderSettings.GetForCurrentTenantAsync(cancellationToken);
        var leadDays = settings?.LeadWorkingDays ?? OrderSettings.DefaultLeadWorkingDays;
        var week = await _businessHours.GetWeekByDayAsync(cancellationToken);
        var today = _clock.GetCurrentInstant().InZone(DateTimeZoneProviders.Tzdb["Asia/Baku"]).Date;

        return BusinessHoursService.NextWorkingDay(week, today, leadDays);
    }

    /// <summary>How far ahead an order may be placed. A phone order for water is for this week,
    /// and a date beyond this is a mishearing rather than a request — a caller was offered and
    /// accepted the 31st of December 2031, which is a Wednesday and so passed a weekday check
    /// with nothing else to stop it.</summary>
    private const int MaxDeliveryDaysAhead = 30;

    /// <summary>Whether a day the caller asked for instead is one the business works, and is a
    /// day it makes sense to promise at all. "Birigün olar?" is a normal thing to say, and the
    /// answer has to come from the tenant's days rather than the model's sense of the
    /// calendar.</summary>
    public async Task<bool> IsWorkingDayAsync(LocalDate day, CancellationToken cancellationToken)
        => await CheckDeliveryDayAsync(day, cancellationToken) == DeliveryDayVerdict.Ok;

    /// <summary>Why a day will not do, so the agent can say the right thing. "We are closed that
    /// day" is a different sentence from "did you mean this year?".</summary>
    public async Task<DeliveryDayVerdict> CheckDeliveryDayAsync(LocalDate day, CancellationToken cancellationToken)
    {
        var today = _clock.GetCurrentInstant().InZone(DateTimeZoneProviders.Tzdb["Asia/Baku"]).Date;
        if (day < today)
        {
            return DeliveryDayVerdict.InThePast;
        }

        if (day > today.PlusDays(MaxDeliveryDaysAhead))
        {
            return DeliveryDayVerdict.TooFarAhead;
        }

        var week = await _businessHours.GetWeekByDayAsync(cancellationToken);
        return week.TryGetValue(day.DayOfWeek, out var hours) && !hours.IsClosed
            ? DeliveryDayVerdict.Ok
            : DeliveryDayVerdict.Closed;
    }

    /// <summary>The Orders page. Reads the whole thing in three queries rather than one per
    /// order's lines — a phone-order business has a lot of small orders.</summary>
    public async Task<IReadOnlyList<OrderResponse>> ListAsync(CancellationToken cancellationToken)
    {
        var orders = await _uow.Orders.GetAllAsync(cancellationToken);
        if (orders.Count == 0)
        {
            return [];
        }

        var products = (await _uow.Products.GetAllAsync(cancellationToken)).ToDictionary(p => p.Id);
        var units = (await _uow.Units.GetAllAsync(cancellationToken)).ToDictionary(u => u.Id, u => u.Name);
        var customers = (await _uow.Customers.GetAllAsync(cancellationToken)).ToDictionary(c => c.Id);

        var responses = new List<OrderResponse>(orders.Count);
        foreach (var order in orders)
        {
            var addresses = await _uow.Customers.GetAddressesAsync(order.CustomerId, cancellationToken);
            var address = addresses.FirstOrDefault(a => a.Id == order.CustomerAddressId);

            var lines = order.Lines.Select(l =>
            {
                var product = products.GetValueOrDefault(l.ProductId);
                var unit = product is null ? string.Empty : units.GetValueOrDefault(product.MeasurementUnitId, string.Empty);
                return new OrderLineResponse(
                    product?.Name ?? $"#{l.ProductId}", l.Quantity, unit, (product?.UnitPrice ?? 0m) * l.Quantity);
            }).ToList();

            responses.Add(new OrderResponse(
                order.Id,
                order.CustomerId,
                customers.GetValueOrDefault(order.CustomerId)?.Name,
                address is null ? string.Empty : CustomerAddressResponse.From(address).Spoken(),
                order.OrderStatus.ToString(),
                order.PlacedAt,
                order.RequestedDeliveryDate,
                order.Notes,
                lines,
                lines.Sum(l => l.LineTotal)));
        }

        return responses;
    }

    /// <summary>The Customers page: who has ordered, with every number and address they gave.</summary>
    public async Task<IReadOnlyList<CustomerResponse>> ListCustomersAsync(CancellationToken cancellationToken)
    {
        var customers = await _uow.Customers.GetAllAsync(cancellationToken);
        var responses = new List<CustomerResponse>(customers.Count);
        foreach (var customer in customers)
        {
            var phones = await _uow.Customers.GetPhoneNumbersAsync(customer.Id, cancellationToken);
            var addresses = await _uow.Customers.GetAddressesAsync(customer.Id, cancellationToken);
            responses.Add(new CustomerResponse(
                customer.Id,
                customer.Name,
                phones.Select(p => p.PhoneNumber).ToList(),
                addresses.Select(a => CustomerAddressResponse.From(a).Spoken()).ToList(),
                customer.CreatedAt));
        }

        return responses;
    }

    public async Task<OrderSettingsResponse> GetSettingsAsync(CancellationToken cancellationToken)
    {
        var settings = await _uow.OrderSettings.GetForCurrentTenantAsync(cancellationToken);
        return new OrderSettingsResponse(settings?.LeadWorkingDays ?? OrderSettings.DefaultLeadWorkingDays);
    }

    /// <summary>Creates the row on first save rather than for every tenant up front — a tenant
    /// without the module has no opinion about delivery.</summary>
    public async Task<OrderSettingsResponse> UpdateSettingsAsync(
        UpdateOrderSettingsRequest request, CancellationToken cancellationToken)
    {
        var tenantId = _currentTenant.TenantId
            ?? throw new InvalidOperationException("This operation requires a tenant-scoped caller.");

        var now = _clock.GetCurrentInstant();
        var settings = await _uow.OrderSettings.GetForCurrentTenantAsync(cancellationToken);
        if (settings is null)
        {
            settings = OrderSettings.Create(tenantId, request.LeadWorkingDays, now);
            await _uow.OrderSettings.AddAsync(settings, cancellationToken);
        }
        else
        {
            settings.SetLeadWorkingDays(request.LeadWorkingDays, now);
        }

        await _uow.SaveChangesAsync(cancellationToken);
        return new OrderSettingsResponse(settings.LeadWorkingDays);
    }

    public async Task<Order> PlaceAsync(
        int customerId, int addressId, IReadOnlyList<(int ProductId, decimal Quantity)> lines,
        LocalDate? requestedDeliveryDate, string? notes, CancellationToken cancellationToken)
    {
        if (lines.Count == 0)
        {
            throw new InvalidOperationException("An order needs at least one line.");
        }

        var tenantId = _currentTenant.TenantId
            ?? throw new InvalidOperationException("This operation requires a tenant-scoped caller.");

        var now = _clock.GetCurrentInstant();
        var order = Order.Place(tenantId, customerId, addressId, requestedDeliveryDate, notes, now);
        foreach (var (productId, quantity) in lines)
        {
            order.AddLine(productId, quantity, now);
        }

        await _uow.Orders.AddAsync(order, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        // The identity is assigned by the insert, so a non-positive id here means the row is not
        // in the database however this method returned. Throwing rather than returning is the
        // point: the caller must not be able to treat "no order number" as a quiet success and
        // tell someone their water is on its way.
        if (order.Id <= 0)
        {
            throw new InvalidOperationException(
                "The order was not persisted — no order number was assigned. Nothing may be confirmed to the caller.");
        }

        return order;
    }
}
