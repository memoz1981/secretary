using Secretary.Domain.Abstractions;
using Secretary.Domain.Enums;
using NodaTime;

namespace Secretary.Domain.Entities;

/// <summary>One phone order, against one address.
///
/// The delivery address is recorded on the order rather than read from the customer at delivery
/// time: a customer with two addresses picks one per order, and next month's order to the other
/// address must not rewrite this one.
///
/// There is no promised slot. A business tells the caller "sabah və ya birigün" — that is a
/// policy sentence, not logistics, and modelling capacity and windows for it would be a
/// different product. RequestedDeliveryDate holds a day only, and only when the caller asks for
/// one.</summary>
public sealed class Order : BaseEntity
{
    public int TenantId { get; private set; }
    public int CustomerId { get; private set; }
    public int CustomerAddressId { get; private set; }
    public OrderStatus OrderStatus { get; private set; }
    public Instant PlacedAt { get; private set; }
    public LocalDate? RequestedDeliveryDate { get; private set; }
    public string? Notes { get; private set; }

    private readonly List<OrderLine> _lines = [];

    /// <summary>An order without lines is not an order — the aggregate owns them so it cannot be
    /// saved half-formed.</summary>
    public IReadOnlyList<OrderLine> Lines => _lines;

    private Order()
    {
    }

    public static Order Place(
        int tenantId, int customerId, int customerAddressId, LocalDate? requestedDeliveryDate, string? notes,
        Instant now)
    {
        var order = new Order
        {
            TenantId = tenantId,
            CustomerId = customerId,
            CustomerAddressId = customerAddressId,
            OrderStatus = OrderStatus.Placed,
            PlacedAt = now,
            RequestedDeliveryDate = requestedDeliveryDate,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
        };

        order.InitBase(now);
        return order;
    }

    public void AddLine(int productId, decimal quantity, Instant now)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be positive.");
        }

        // The caller who says "two bidons, and another one" means three, not two lines.
        var existing = _lines.FirstOrDefault(l => l.ProductId == productId);
        if (existing is not null)
        {
            existing.Add(quantity, now);
        }
        else
        {
            _lines.Add(OrderLine.Create(productId, quantity, now));
        }

        Touch(now);
    }

    public void MarkDelivered(Instant now)
    {
        Transition(OrderStatus.Delivered, now);
    }

    public void Cancel(Instant now)
    {
        Transition(OrderStatus.Cancelled, now);
    }

    private void Transition(OrderStatus next, Instant now)
    {
        if (OrderStatus != OrderStatus.Placed)
        {
            throw new InvalidOperationException($"An order that is already {OrderStatus} cannot become {next}.");
        }

        OrderStatus = next;
        Touch(now);
    }
}
