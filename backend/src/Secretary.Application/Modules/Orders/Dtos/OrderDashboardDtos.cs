using Secretary.Domain.Enums;

namespace Secretary.Application.Dtos;

/// <summary>One call outcome, its count, and its share of every order call in the range.
///
/// ⚠ The share is computed on the server, exactly as the appointment dashboard does it. Tiles
/// each dividing by a total handed to them separately is one chance per tile to divide by a
/// different number, and the first symptom is percentages that do not add to 100 on a customer's
/// screen.</summary>
public sealed record OrderCallOutcomeCount(CallOutcome Outcome, int Calls, double Share);

/// <summary>One product and how much of it was ordered in the range.</summary>
/// <param name="Quantity">In the product's own unit — bidons, litres, boxes. Not comparable
/// across rows, which is why the chart beside it is drawn on Orders rather than on this.</param>
public sealed record ProductDemand(int ProductId, string ProductName, int Orders, decimal Quantity, decimal ValueAzn);

/// <summary>What the ordering line did over a range.
///
/// ⚠ Built on order calls, not on orders. The question this page answers is how the agent is
/// doing, and an orders-only view cannot see the calls that produced nothing — which are the
/// rows worth reading. Orders come in for the money figures, matched back by RelatedOrderId.</summary>
public sealed record OrderDashboardResponse(
    int TotalCalls,

    /// <summary>Every outcome the module can record, whether or not it occurred. A tile that
    /// vanishes at zero moves the ones beside it, so the page reshuffles between one range and
    /// the next and a reader loses the positions they had learned.</summary>
    IReadOnlyList<OrderCallOutcomeCount> ByOutcome,

    /// <summary>Calls that ended with an order on the books. Not the same as a resolved call: the
    /// agent can answer a question perfectly and take no order, and that is a success by every
    /// measure except this one.</summary>
    int CallsWithOrder,

    int OrdersPlaced,
    int OrdersCancelled,
    decimal TotalValueAzn,

    /// <summary>Null when nothing was ordered. An average over no orders is not zero — zero would
    /// read as a line taking orders worth nothing.</summary>
    decimal? AverageOrderValueAzn,

    int TotalDurationSeconds,
    double AverageCallDurationSeconds,

    /// <summary>Over calls that produced an order. A line whose failures are three seconds long
    /// looks fast in a blended average, which is the opposite of what the number is for.</summary>
    double? AverageOrderingCallDurationSeconds,

    /// <summary>What people actually asked for, busiest first. The catalogue's long tail is the
    /// useful part — a product nobody orders is a product to stop stocking.</summary>
    IReadOnlyList<ProductDemand> ByProduct)
{
    /// <summary>Of every call in the range. Null on no calls rather than 0%, which would read as
    /// a line that never converts rather than one nobody rang.</summary>
    public double? OrderRate => TotalCalls == 0 ? null : (double)CallsWithOrder / TotalCalls;
}
