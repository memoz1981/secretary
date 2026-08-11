using System.ComponentModel;
using Secretary.Agents.Orders;
using Secretary.Application.Abstractions;
using Secretary.Application.Dtos;
using Secretary.Application.Services;
using Microsoft.Extensions.Logging;

namespace Secretary.Agents.Tools;

/// <summary>One line of an order tool's result. Ids, never names: the id came out of
/// ListProducts moments earlier, so there is nothing for the model to spell and nothing for the
/// Azerbaijani fold to get wrong.</summary>
public sealed record OrderLineInput(
    [property: Description("Product id from ListProducts")] int ProductId,
    [property: Description("How many")] decimal Quantity);

/// <summary>The order line's tools. Results are DATA — never sentences telling the model how to
/// behave, because anything phrased as instruction gets read aloud to the caller. Order.md says
/// what to do with each marker.
///
/// Seven tools where there were eleven, and the shape changed more than the count. Identity is
/// three plain lookups whose result the agent reads back out loud instead of a lookup, a
/// generated challenge and a confirm tool. The order arrives in one call rather than one call
/// per product. The delivery day is assigned here rather than negotiated. Together that is most
/// of the instruction file, which was ~70% of what every model response paid for.</summary>
public sealed class OrderTools
{
    private readonly CustomerIdentityService _identity;
    private readonly OrderService _orders;
    private readonly ITenantOrderDirectory _directory;
    private readonly OrderCallSession _session;
    private readonly EscalationTools _escalation;
    private readonly ILogger<OrderTools> _logger;

    public OrderTools(
        CustomerIdentityService identity, OrderService orders, ITenantOrderDirectory directory,
        OrderCallSession session, EscalationTools escalation, ILogger<OrderTools> logger)
    {
        _identity = identity;
        _orders = orders;
        _directory = directory;
        _session = session;
        _escalation = escalation;
        _logger = logger;
    }

    [Description("Finds the caller by the customer number they quoted.")]
    public async Task<string> FindCustomerById(
        [Description("The customer number they said")] int customerId)
        => Describe(await _identity.FindByIdAsync(customerId, default));

    [Description("Finds the caller by their phone number.")]
    public async Task<string> FindCustomerByPhone(
        [Description("Their phone number as they said it")] string phoneNumber)
        => Describe(await _identity.FindByPhoneAsync(phoneNumber, default));

    [Description("Finds the caller by where they live. Only after a customer number and a phone number both failed.")]
    public async Task<string> FindCustomerByAddress(
        [Description("Baku rayon, e.g. Xətai")] string district,
        [Description("Street name only, e.g. Sarayevo")] string street)
        => Describe(await _identity.FindByAddressAsync(district, street, default));

    /// <summary>One shape for all three lookups, which is the point: the instruction file
    /// describes these three markers once instead of a ladder of outcomes per route.
    ///
    /// Nobody is identified here. The agent reads the name and the rayon back and the caller
    /// agrees or does not — a misheard digit is caught by the same person who would have
    /// answered a challenge question, one turn earlier.</summary>
    private string Describe(IReadOnlyList<CallerMatch> matches)
    {
        if (matches.Count == 0)
        {
            return "NOT_FOUND.";
        }

        if (matches.Count > 1)
        {
            // Several people. Each with the one address line that tells them apart, and their
            // customer number so the agent can come back through FindCustomerById with an id it
            // was given rather than one it read out of what the caller said.
            var candidates = matches.Select(m =>
                $"{m.CustomerId} {m.Name ?? "adsız"} — {m.Addresses[0].District} r., {m.Addresses[0].Street}");

            return $"MANY. {string.Join("; ", candidates)}";
        }

        var match = matches[0];
        var where = match.Addresses.Count == 1
            ? $"Ünvan {match.Addresses[0].Id}: {match.Addresses[0].Spoken()}"
            : "Ünvanlar: " + string.Join("; ", match.Addresses.Select(a => $"{a.Id} — {a.Spoken()}"));

        // Remembered for the call log. Not proof they are who they say — the caller still has
        // to agree out loud — but a browser call carries no number to look them up by
        // afterwards, so this is the only moment the record can be attributed to a person.
        _session.Identified(match.CustomerId);

        return $"FOUND. Müştəri {match.CustomerId}, {match.Name ?? "adsız"}. {where}";
    }

    [Description("Records a first-time caller and returns their customer number.")]
    public async Task<string> RegisterCustomer(
        [Description("Their name")] string name,
        [Description("Their phone number")] string phoneNumber,
        [Description("Baku rayon, e.g. Nəsimi")] string district,
        [Description("Street name")] string street,
        [Description("Building number, e.g. 12A")] string building,
        [Description("Apartment number, empty for a private house")] string? apartment)
    {
        try
        {
            var result = await _identity.RegisterAsync(
                new NewCustomerDetails(name, phoneNumber, district, street, building, apartment), default);

            _session.Identified(result.CustomerId);
            return $"REGISTERED. Müştəri {result.CustomerId}.";
        }
        catch (ArgumentException ex)
        {
            // Almost always the rayon: the model heard somewhere that is not one of the twelve.
            return $"NOT_REGISTERED. {ex.Message}";
        }
    }

    [Description("What this business sells, with the product id needed to order it.")]
    public async Task<string> ListProducts()
    {
        var catalog = await _directory.GetProductsAsync(default);
        if (catalog.Count == 0)
        {
            return "NO_PRODUCTS.";
        }

        return string.Join("; ", catalog.Select(p =>
        {
            var cap = p.MaxOrderQuantity is { } max ? $", maks {Trim(max)} {p.Unit}" : string.Empty;
            return $"{p.Id} {p.Name} — {p.UnitPrice:0.##} AZN / {p.Unit}{cap}";
        }));
    }

    [Description("Places the whole order at once and returns the delivery day. Call it after the caller has said "
                 + "everything they want.")]
    public async Task<string> PlaceOrder(
        [Description("The customer number")] int customerId,
        [Description("Address id from the find result; 0 if they only have one")] int addressId,
        [Description("Every product and quantity they asked for")] OrderLineInput[] lines)
    {
        if (lines is null || lines.Length == 0)
        {
            return "EMPTY_ORDER.";
        }

        var catalog = await _directory.GetProductsAsync(default);
        var byId = catalog.ToDictionary(p => p.Id);

        // Merged before anything is checked: a caller who says three bidons and then another two
        // is asking for five, and a cap judged one line at a time would wave that through.
        var wanted = new Dictionary<int, decimal>();
        foreach (var line in lines)
        {
            if (line.Quantity <= 0)
            {
                return "BAD_QUANTITY.";
            }

            if (!byId.ContainsKey(line.ProductId))
            {
                return $"NO_SUCH_PRODUCT. {line.ProductId}. "
                       + string.Join("; ", catalog.Select(p => $"{p.Id} {p.Name}"));
            }

            wanted[line.ProductId] = wanted.GetValueOrDefault(line.ProductId) + line.Quantity;
        }

        foreach (var (productId, quantity) in wanted)
        {
            var product = byId[productId];
            if (product.MaxOrderQuantity is { } cap && quantity > cap)
            {
                return $"OVER_MAXIMUM. {product.Name}: {Trim(cap)} {product.Unit} maks.";
            }
        }

        var addresses = await _identity.GetAddressesAsync(customerId, default);
        if (addresses.Count == 0)
        {
            return "NO_ADDRESS_ON_FILE.";
        }

        // The id has to be checked against this customer's own addresses rather than trusted.
        // It used to be resolved entirely server-side because the model filled it with 43 — the
        // mənzil out of "Xətai r., Sarayevo, ev 12, mənzil 43" — and a caller with two addresses
        // now has to be able to choose. Rejecting an id that is not theirs costs one retry; not
        // checking it delivers to somebody else's street.
        var address = addressId <= 0
            ? addresses.FirstOrDefault(a => a.IsDefault) ?? addresses[0]
            : addresses.FirstOrDefault(a => a.Id == addressId);

        if (address is null)
        {
            return "NO_SUCH_ADDRESS. " + string.Join("; ", addresses.Select(a => $"{a.Id} — {a.Spoken()}"));
        }

        var deliveryDay = await _directory.GetDeliveryDayAsync(default);
        if (deliveryDay is null)
        {
            return "NO_WORKING_DAY.";
        }

        // Everything below exists so this tool can never hand back something a model could read
        // as success without an order number behind it.
        //
        // On the appointment line the same models have confirmed a booking that was never made,
        // three times in one day, and invented a caller's phone number. Instruction alone does
        // not stop it — its first line already forbids exactly this. What can stop it is never
        // producing a string that looks like a confirmation unless a row exists.
        try
        {
            var order = await _orders.PlaceAsync(
                customerId, address.Id, wanted.Select(kv => (kv.Key, kv.Value)).ToList(), deliveryDay, notes: null,
                default);

            if (order.Id <= 0)
            {
                return await FailWithoutConfirming(customerId, "The order returned no order number.", exception: null);
            }

            _session.Placed(order.Id);
            _session.Identified(customerId);

            // The readback comes from here rather than from the model's memory of the
            // conversation. That is the whole reason the order is one call: what it repeats to
            // the caller is what went into the database, not what it believes it heard.
            var what = string.Join(", ", wanted.Select(kv => Say(byId[kv.Key], kv.Value)));
            return $"ORDER_PLACED. Sifariş {order.Id}: {what}. Çatdırılma: {AzerbaijanTime.SpokenDate(deliveryDay.Value)}";
        }
        catch (Exception ex)
        {
            return await FailWithoutConfirming(customerId, ex.Message, ex);
        }
    }

    [Description("Cancels the order just placed on this call, so a corrected one can replace it.")]
    public async Task<string> CancelOrder(
        [Description("The order number PlaceOrder returned")] int orderId,
        [Description("The customer number")] int customerId)
    {
        if (!_session.CanCancel(orderId))
        {
            return "NOT_THIS_CALL.";
        }

        if (!await _orders.CancelPlacedAsync(orderId, customerId, default))
        {
            return "NOT_CANCELLED.";
        }

        _session.Cancelled();
        return $"ORDER_CANCELLED. Sifariş {orderId}";
    }

    /// <summary>One order line the way it is said out loud: "3 ədəd Sirab".
    ///
    /// It used to read "Sirab × 3", and the agent said "Sirab vuraq üç" — it read the symbol. A
    /// tool result is spoken almost verbatim, so it has to be written the way a person would say
    /// it, in the order they would say it, with the unit the product is actually sold in.</summary>
    private static string Say(ProductResponse product, decimal quantity)
        => $"{Trim(quantity)} {product.Unit} {product.Name}".Replace("  ", " ");

    /// <summary>Three, not 3.000 — the agent reads this aloud.</summary>
    private static string Trim(decimal quantity)
        => quantity == decimal.Truncate(quantity) ? ((long)quantity).ToString() : quantity.ToString("0.###");

    /// <summary>The order did not happen. Get a person on the line and give the model nothing it
    /// could mistake for a confirmation — no order number, no products, no day.</summary>
    private async Task<string> FailWithoutConfirming(int customerId, string reason, Exception? exception)
    {
        _logger.LogError(exception, "Order for customer {CustomerId} was not placed: {Reason}", customerId, reason);

        // Before anything else that writes. The rejected order is still tracked, and without
        // this the escalation below re-submits it, fails for the same reason, and the caller
        // ends up neither transferred nor recorded — which is exactly what happened.
        _orders.DiscardPendingChanges();

        try
        {
            var phone = (await _identity.GetPhoneNumbersAsync(customerId, default)).FirstOrDefault()
                        ?? "unknown — ask the caller";

            await _escalation.EscalateToHuman(phone, $"Order could not be placed: {reason}");
            return "ORDER_FAILED. TRANSFER_ALREADY_STARTED.";
        }
        catch (Exception escalationFailure)
        {
            _logger.LogError(escalationFailure, "Escalating a failed order also failed.");
            return "ORDER_FAILED. TRANSFER_FAILED.";
        }
    }
}
