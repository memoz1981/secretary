using Secretary.Application.Abstractions;
using Secretary.Application.Abstractions.Persistence;
using Secretary.Application.Dtos;
using Secretary.Domain.Entities;
using Secretary.Domain.Enums;
using Secretary.Domain.Exceptions;
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
    private readonly IAgentDirectoryChangeNotifier _changeNotifier;

    public OrderService(
        IUnitOfWork uow, BusinessHoursService businessHours, IClock clock, ICurrentTenantProvider currentTenant,
        IAgentDirectoryChangeNotifier changeNotifier)
    {
        _uow = uow;
        _businessHours = businessHours;
        _clock = clock;
        _currentTenant = currentTenant;
        _changeNotifier = changeNotifier;
    }

    /// <summary>For a caller recovering from a failed write — see IUnitOfWork.</summary>
    public void DiscardPendingChanges() => _uow.DiscardPendingChanges();

    public async Task<IReadOnlyList<ProductResponse>> GetCatalogAsync(CancellationToken cancellationToken)
    {
        var products = await _uow.Products.GetCatalogAsync(cancellationToken);
        var units = await _uow.Units.GetAllAsync(cancellationToken);
        var unitNames = units.ToDictionary(u => u.Id, u => u.Name);

        return products
            .Select(p => new ProductResponse(
                p.Id, p.Name, unitNames.GetValueOrDefault(p.MeasurementUnitId, string.Empty), p.UnitPrice,
                p.MaxOrderQuantity))
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

    /// <summary>Whether a day the caller asked for instead is one the business works, and is a
    /// day it makes sense to promise at all. "Birigün olar?" is a normal thing to say, and the
    /// answer has to come from the tenant's days rather than the model's sense of the
    /// calendar.</summary>
    public async Task<bool> IsWorkingDayAsync(LocalDate day, CancellationToken cancellationToken)
        => await CheckDeliveryDayAsync(day, cancellationToken) == DeliveryDayVerdict.Ok;

    /// <summary>The window is closed at both ends: the lead time is where it starts, the cap is
    /// where it ends, and nothing outside is bookable. The lead used only to pick the day the
    /// agent offered first, which meant any caller naming an earlier date got it — a lead time
    /// that anyone can undercut on request is not a lead time.</summary>
    public async Task<DeliveryDayVerdict> CheckDeliveryDayAsync(LocalDate day, CancellationToken cancellationToken)
    {
        var today = _clock.GetCurrentInstant().InZone(DateTimeZoneProviders.Tzdb["Asia/Baku"]).Date;
        if (day < today)
        {
            return DeliveryDayVerdict.InThePast;
        }

        var settings = await _uow.OrderSettings.GetForCurrentTenantAsync(cancellationToken);
        var window = settings?.MaxDeliveryDaysAhead ?? OrderSettings.DefaultMaxDeliveryDaysAhead;
        if (day > today.PlusDays(window))
        {
            return DeliveryDayVerdict.TooFarAhead;
        }

        var week = await _businessHours.GetWeekByDayAsync(cancellationToken);

        // The floor is the first day the business would offer, not the raw lead number — the
        // lead counts working days and the cap counts calendar days, so comparing the two
        // numbers directly is only right when the business never closes.
        var leadDays = settings?.LeadWorkingDays ?? OrderSettings.DefaultLeadWorkingDays;
        var earliest = BusinessHoursService.NextWorkingDay(week, today, leadDays);
        if (earliest is not null && day < earliest)
        {
            return DeliveryDayVerdict.TooSoon;
        }

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

    /// <summary>Marks an order delivered or cancelled from the Orders page. The one thing staff
    /// do to an order after the call: an order is taken by phone, but whether it arrived is
    /// something only a person knows.</summary>
    public async Task<OrderResponse> SetStatusAsync(int id, OrderStatus status, CancellationToken cancellationToken)
    {
        var order = await _uow.Orders.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException(nameof(Order), id);

        var now = _clock.GetCurrentInstant();
        switch (status)
        {
            case OrderStatus.Delivered:
                order.MarkDelivered(now);
                break;
            case OrderStatus.Cancelled:
                order.Cancel(now);
                break;
            default:
                throw new InvalidOperationException($"An order cannot be set back to {status}.");
        }

        await _uow.SaveChangesAsync(cancellationToken);
        return (await ListAsync(cancellationToken)).First(o => o.Id == id);
    }

    public async Task<OrderSettingsResponse> GetSettingsAsync(CancellationToken cancellationToken)
    {
        var settings = await _uow.OrderSettings.GetForCurrentTenantAsync(cancellationToken);
        return new OrderSettingsResponse(
            settings?.LeadWorkingDays ?? OrderSettings.DefaultLeadWorkingDays,
            settings?.MaxDeliveryDaysAhead ?? OrderSettings.DefaultMaxDeliveryDaysAhead);
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
            settings = OrderSettings.Create(
                tenantId, request.LeadWorkingDays, request.MaxDeliveryDaysAhead, now);
            await _uow.OrderSettings.AddAsync(settings, cancellationToken);
        }
        else
        {
            settings.Update(request.LeadWorkingDays, request.MaxDeliveryDaysAhead, now);
        }

        await _uow.SaveChangesAsync(cancellationToken);

        // The lead time decides the day the agent promises, and it is cached with the working
        // week. A tenant who lengthens it and is still quoted yesterday's lead has made a
        // promise they cannot keep.
        _changeNotifier.NotifyChanged(tenantId);

        return new OrderSettingsResponse(settings.LeadWorkingDays, settings.MaxDeliveryDaysAhead);
    }

    /// <summary>Cancels an order the agent placed moments ago, so a corrected one can replace it.
    ///
    /// Belt and braces with the call session that gates the tool: this also refuses an order
    /// belonging to somebody else and one that has already moved on. False rather than an
    /// exception, because "that is not yours" is an answer the agent has to say out loud, not a
    /// failure to escalate.</summary>
    public async Task<bool> CancelPlacedAsync(int orderId, int customerId, CancellationToken cancellationToken)
    {
        var order = await _uow.Orders.GetByIdAsync(orderId, cancellationToken);
        if (order is null || order.CustomerId != customerId || order.OrderStatus != OrderStatus.Placed)
        {
            return false;
        }

        order.Cancel(_clock.GetCurrentInstant());
        await _uow.SaveChangesAsync(cancellationToken);
        return true;
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
