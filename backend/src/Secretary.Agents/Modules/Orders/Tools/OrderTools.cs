using System.ComponentModel;
using Secretary.Agents.Orders;
using Secretary.Application.Dtos;
using Secretary.Application.Services;
using Microsoft.Extensions.Logging;
using NodaTime;

namespace Secretary.Agents.Tools;

/// <summary>The order line's tools. Results are DATA — never sentences telling the model how to
/// behave, because anything phrased as instruction gets read aloud to the caller. Order.md says
/// what to do with each marker.</summary>
public sealed class OrderTools
{
    private readonly CustomerIdentityService _identity;
    private readonly OrderService _orders;
    private readonly OrderDraft _draft;
    private readonly EscalationTools _escalation;
    private readonly ILogger<OrderTools> _logger;

    public OrderTools(
        CustomerIdentityService identity, OrderService orders, OrderDraft draft, EscalationTools escalation,
        ILogger<OrderTools> logger)
    {
        _identity = identity;
        _orders = orders;
        _draft = draft;
        _escalation = escalation;
        _logger = logger;
    }

    [Description("Finds the caller by their customer number, or by their phone number. Never identifies them on " +
                 "its own — it returns the one question to ask before trusting the match.")]
    public async Task<string> FindCustomer(
        [Description("The customer number they quoted, or 0 if they did not give one")] int customerId,
        [Description("Their phone number as they said it, or empty if they gave a customer number instead")] string phoneNumber)
    {
        var result = await _identity.FindAsync(customerId, phoneNumber, default);
        return result.Outcome switch
        {
            CallerIdentityOutcome.NotFound => "NEW_CALLER.",

            // Distinct from NEW_CALLER on purpose. They quoted a number, so they have one; the
            // digit is far likelier to be misheard than invented, and registering them again
            // would give one person two records and two customer numbers.
            CallerIdentityOutcome.NoSuchCustomer => $"NO_SUCH_CUSTOMER. {result.Challenge}",
            CallerIdentityOutcome.Ambiguous => $"AMBIGUOUS. {result.Challenge}",
            CallerIdentityOutcome.NeedsConfirmation =>
                $"CONFIRM_NEEDED. Customer {result.CustomerId}. {result.Challenge}",
            _ => "NEW_CALLER.",
        };
    }

    [Description("Checks what the caller answered against their record. Call this with their exact words. Only " +
                 "after this returns IDENTIFIED is the caller known.")]
    public async Task<string> ConfirmCustomer(
        [Description("The customer number from FindCustomer")] int customerId,
        [Description("Exactly what the caller said, in their words")] string spokenAnswer)
    {
        var result = await _identity.ConfirmAsync(customerId, spokenAnswer, default);
        if (result.Outcome != CallerIdentityOutcome.Identified)
        {
            return $"NOT_CONFIRMED. {result.Challenge}";
        }

        var addresses = await _identity.GetAddressesAsync(customerId, default);
        var where = addresses.Count switch
        {
            0 => "No address on file.",
            1 => $"Address {addresses[0].Id}: {addresses[0].Spoken()}",
            _ => "Addresses: " + string.Join("; ", addresses.Select(a => $"{a.Id} — {a.Spoken()}")),
        };

        return $"IDENTIFIED. Customer {customerId}, {result.CustomerName ?? "no name on file"}. {where}";
    }

    [Description("Records a caller who has never ordered before, and returns the customer number to read back " +
                 "to them. Only call this once every field has been heard and repeated back.")]
    public async Task<string> RegisterCustomer(
        [Description("Their name")] string name,
        [Description("Their phone number")] string phoneNumber,
        [Description("Baku rayon, e.g. Nəsimi")] string district,
        [Description("Qəsəbə or massiv, empty if none")] string? area,
        [Description("Street name")] string street,
        [Description("Döngə, e.g. 5-ci döngə — empty if none")] string? lane,
        [Description("Building number, e.g. 12A")] string building,
        [Description("Apartment number, empty for a private house")] string? apartment,
        [Description("A landmark the driver would use, empty if none")] string? landmark,
        [Description("The whole address exactly as the caller said it")] string? spokenAddress)
    {
        try
        {
            var customer = await _identity.RegisterAsync(
                new NewCustomerDetails(
                    name, phoneNumber, district, area, street, lane, building, apartment, landmark, spokenAddress),
                default);

            return $"REGISTERED. Customer {customer.Id}.";
        }
        catch (ArgumentException ex)
        {
            // Almost always the rayon: the model heard somewhere that is not one of the twelve.
            return $"NOT_REGISTERED. {ex.Message}";
        }
    }

    [Description("What this business sells, with prices and units.")]
    public async Task<string> GetProductCatalog()
    {
        var catalog = await _orders.GetCatalogAsync(default);
        return catalog.Count == 0
            ? "No products."
            : string.Join("; ", catalog.Select(p => $"{p.Name} — {p.UnitPrice:0.##} AZN / {p.Unit}"));
    }

    [Description("Adds what the caller asked for to the order. Call it once per product as they say it.")]
    public async Task<string> AddToOrder(
        [Description("The product as the caller named it")] string productName,
        [Description("How many or how much")] decimal quantity)
    {
        if (quantity <= 0)
        {
            return "BAD_QUANTITY.";
        }

        var product = await _orders.MatchProductAsync(productName, default);
        if (product is null)
        {
            var catalog = await _orders.GetCatalogAsync(default);
            return $"NO_SUCH_PRODUCT. Products: {string.Join(", ", catalog.Select(p => p.Name))}.";
        }

        _draft.Add(product.Id, product.Name, quantity);
        return $"Added. Order so far: {_draft.Describe()}";
    }

    [Description("Corrects a line to an exact quantity, or removes it with a quantity of zero.")]
    public async Task<string> SetOrderQuantity(
        [Description("The product as the caller named it")] string productName,
        [Description("The quantity it should now be; zero removes it")] decimal quantity)
    {
        var product = await _orders.MatchProductAsync(productName, default);
        if (product is null)
        {
            return "NO_SUCH_PRODUCT.";
        }

        _draft.Set(product.Id, product.Name, quantity);
        return $"Updated. Order so far: {_draft.Describe()}";
    }

    [Description("The day this business can deliver. Call with an empty day to get the soonest; call with a day " +
                 "the caller asked for instead to find out whether it works.")]
    public async Task<string> GetDeliveryDay(
        [Description("A day the caller asked for, as 2026-08-10 — empty for the soonest")] string? requestedDayLocal)
    {
        if (!string.IsNullOrWhiteSpace(requestedDayLocal))
        {
            if (!AzerbaijanTime.TryParse($"{requestedDayLocal.Trim()} 12:00", out var parsed))
            {
                return "UNREADABLE_DAY.";
            }

            var asked = parsed.InZone(AzerbaijanTime.Zone).Date;
            return await _orders.IsWorkingDayAsync(asked, default)
                ? $"DAY_OK. {asked:yyyy-MM-dd}"
                : $"CLOSED_THAT_DAY. {asked:yyyy-MM-dd}";
        }

        var soonest = await _orders.ProposeDeliveryDayAsync(default);
        return soonest is null
            ? "NO_WORKING_DAY."
            : $"DELIVERY_DAY. {soonest.Value:yyyy-MM-dd}";
    }

    [Description("Places the order. Only after the caller has confirmed what they want and which day.")]
    public async Task<string> PlaceOrder(
        [Description("The confirmed customer number")] int customerId,
        [Description("The delivery address id, from ConfirmCustomer")] int addressId,
        [Description("Delivery day as 2026-08-10, from GetDeliveryDay")] string deliveryDayLocal,
        [Description("Anything else the caller mentioned, empty if nothing")] string? notes)
    {
        if (_draft.IsEmpty)
        {
            return "EMPTY_ORDER.";
        }

        LocalDate? deliveryDay = null;
        if (!string.IsNullOrWhiteSpace(deliveryDayLocal))
        {
            if (!AzerbaijanTime.TryParse($"{deliveryDayLocal.Trim()} 12:00", out var parsed))
            {
                return "UNREADABLE_DAY.";
            }

            deliveryDay = parsed.InZone(AzerbaijanTime.Zone).Date;
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
            var order = await _orders.PlaceAsync(customerId, addressId, _draft.Lines, deliveryDay, notes, default);
            if (order.Id <= 0)
            {
                return await FailWithoutConfirming(
                    customerId, "The order returned no order number.", exception: null);
            }

            var day = deliveryDay is null ? string.Empty : $", {deliveryDay.Value:yyyy-MM-dd}";
            return $"ORDER_PLACED. Order {order.Id}: {_draft.Describe()}{day}.";
        }
        catch (Exception ex)
        {
            return await FailWithoutConfirming(customerId, ex.Message, ex);
        }
    }

    /// <summary>The order did not happen. Get a person on the line and give the model nothing it
    /// could mistake for a confirmation — no order number, no products, no day.</summary>
    private async Task<string> FailWithoutConfirming(int customerId, string reason, Exception? exception)
    {
        _logger.LogError(exception, "Order for customer {CustomerId} was not placed: {Reason}", customerId, reason);

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
