using Secretary.Application.Abstractions.Persistence;
using Secretary.Application.Dtos;
using Secretary.Domain.Entities;
using Secretary.Domain.Enums;
using NodaTime;

namespace Secretary.Application.Services;

/// <summary>What the ordering line did over a range.
///
/// ⚠ Built on order calls rather than on orders, and the difference decides what the page can
/// say. An orders-only view cannot see the calls that produced nothing, and those are the rows
/// worth reading — a line taking forty orders from a hundred calls and one taking forty from
/// forty-two are the same page otherwise.
///
/// Counts and sums, divided once at the end. Never an average of averages: that lets a
/// twenty-second call weigh as much as a six-minute one and makes the number swing on call mix
/// rather than on what happened.
///
/// Aggregated in memory like the appointment dashboard, and the same note applies — fine at this
/// scale, and this is the seam to replace with a database-side aggregate if call volume ever
/// makes it matter.</summary>
public sealed class OrderDashboardService
{
    private readonly IUnitOfWork _uow;

    public OrderDashboardService(IUnitOfWork uow) => _uow = uow;

    public async Task<OrderDashboardResponse> GetAsync(Instant from, Instant to, CancellationToken cancellationToken)
    {
        var calls = await _uow.OrderCalls.SearchAsync(from, to, null, cancellationToken);

        // Every order the tenant has, narrowed here. The repository has no range query and this
        // needs the lines anyway, which a range query would not have saved us loading.
        var allOrders = await _uow.Orders.GetAllAsync(cancellationToken);
        var orders = allOrders.Where(o => o.PlacedAt >= from && o.PlacedAt <= to).ToList();

        var totalCalls = calls.Count;

        var byOutcome = Enum.GetValues<CallOutcome>()
            .Select(outcome =>
            {
                var count = calls.Count(c => c.Outcome == outcome);
                return new OrderCallOutcomeCount(outcome, count, Rate(count, totalCalls));
            })
            .ToList();

        // ⚠ Calls that produced an order, which is not the same as calls that resolved. The agent
        // can answer a question perfectly and take no order — a success by every measure but this
        // one — so the two numbers are reported separately rather than one standing for the other.
        var orderingCalls = calls.Where(c => c.RelatedOrderId is not null).ToList();

        var products = (await _uow.Products.GetAllAsync(cancellationToken)).ToDictionary(p => p.Id);
        var live = orders.Where(o => o.OrderStatus != OrderStatus.Cancelled).ToList();
        var totalValue = live.Sum(o => Value(o, products));

        return new OrderDashboardResponse(
            TotalCalls: totalCalls,
            ByOutcome: byOutcome,
            CallsWithOrder: orderingCalls.Count,
            OrdersPlaced: live.Count,
            OrdersCancelled: orders.Count(o => o.OrderStatus == OrderStatus.Cancelled),

            // ⚠ Cancelled orders excluded from the money, counted separately. Their value was
            // never earned, and leaving it in the total would let a day of cancellations read as
            // the best day the line ever had.
            TotalValueAzn: totalValue,
            AverageOrderValueAzn: live.Count == 0 ? null : totalValue / live.Count,

            TotalDurationSeconds: calls.Sum(c => c.DurationSeconds),
            AverageCallDurationSeconds: totalCalls == 0 ? 0 : calls.Average(c => c.DurationSeconds),
            AverageOrderingCallDurationSeconds:
                orderingCalls.Count == 0 ? null : orderingCalls.Average(c => c.DurationSeconds),

            ByProduct: Demand(live, products));
    }

    /// <summary>What was ordered, busiest first.
    ///
    /// Ranked by how many orders asked for it rather than by quantity: quantities are in each
    /// product's own unit, so three bidons and three kilos are not three of the same thing and
    /// sorting on them compares nothing.</summary>
    private static IReadOnlyList<ProductDemand> Demand(
        IReadOnlyList<Order> orders, IReadOnlyDictionary<int, Product> products)
        => orders
            .SelectMany(order => order.Lines)
            .GroupBy(line => line.ProductId)
            .Select(group =>
            {
                var product = products.GetValueOrDefault(group.Key);
                return new ProductDemand(
                    ProductId: group.Key,

                    // A product deleted from the catalogue still appears in the orders it was on.
                    // Named by its id rather than dropped: the money is real and has to add up.
                    ProductName: product?.Name ?? $"#{group.Key}",
                    Orders: group.Count(),
                    Quantity: group.Sum(line => line.Quantity),
                    ValueAzn: group.Sum(line => line.Quantity * (product?.UnitPrice ?? 0m)));
            })
            .OrderByDescending(row => row.Orders)
            .ThenByDescending(row => row.ValueAzn)
            .ToList();

    private static decimal Value(Order order, IReadOnlyDictionary<int, Product> products)
        => order.Lines.Sum(line => line.Quantity * (products.GetValueOrDefault(line.ProductId)?.UnitPrice ?? 0m));

    private static double Rate(int count, int total) => total == 0 ? 0 : (double)count / total;
}
