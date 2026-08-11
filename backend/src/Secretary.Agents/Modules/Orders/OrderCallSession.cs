namespace Secretary.Agents.Orders;

/// <summary>What this call has already done, for the length of one call.
///
/// It holds one thing: the order placed on this call. That is the leash on CancelOrder — order
/// numbers are no more secret than customer numbers, and a cancel tool that accepts any number
/// lets a caller guess at a stranger's delivery. Anyone ringing about an earlier order gets a
/// person instead.
///
/// It replaced the order draft and the identity session, which existed because the order was
/// assembled a product at a time and the caller was identified across several tool calls.
/// Neither is true now: the order arrives in one call and identity is settled out loud.</summary>
public sealed class OrderCallSession
{
    public int? PlacedOrderId { get; private set; }

    public void Placed(int orderId) => PlacedOrderId = orderId;

    /// <summary>Cleared on cancel so the same order cannot be cancelled twice — the second
    /// attempt would throw out of the domain and read to the model as a failure it should
    /// escalate.</summary>
    public void Cancelled() => PlacedOrderId = null;

    public bool CanCancel(int orderId) => PlacedOrderId is { } placed && placed == orderId;
}
