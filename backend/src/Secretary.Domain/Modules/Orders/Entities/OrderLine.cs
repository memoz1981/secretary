using Secretary.Domain.Abstractions;
using NodaTime;

namespace Secretary.Domain.Entities;

/// <summary>One product and how much of it.
///
/// Quantity is decimal, not int: the unit decides. Three bidons is 3, but two and a half kilos
/// is 2.5, and an integer column would silently round a real order.</summary>
public sealed class OrderLine : BaseEntity
{
    public int OrderId { get; private set; }
    public int ProductId { get; private set; }
    public decimal Quantity { get; private set; }

    private OrderLine()
    {
    }

    internal static OrderLine Create(int productId, decimal quantity, Instant now)
    {
        var line = new OrderLine { ProductId = productId, Quantity = quantity };
        line.InitBase(now);
        return line;
    }

    internal void Add(decimal quantity, Instant now)
    {
        Quantity += quantity;
        Touch(now);
    }
}
