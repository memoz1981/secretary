namespace Secretary.Domain.Enums;

/// <summary>Where an order is in its short life. Deliberately shorter than a courier system's:
/// a phone order for water is placed, it arrives, or it doesn't.</summary>
public enum OrderStatus
{
    Placed = 0,
    Delivered = 1,
    Cancelled = 2,
}
