using Secretary.Application.Abstractions.Persistence;
using Secretary.Application.Services;
using Secretary.Domain.Entities;
using Secretary.Domain.Enums;
using Secretary.Domain.ValueObjects;
using Moq;
using NodaTime;
using Shouldly;
using Xunit;

namespace Secretary.Application.Tests;

/// <summary>What the ordering line did over a range.
///
/// ⚠ The figures pinned here are the ones that go wrong quietly. A cancelled order left in the
/// total makes a day of cancellations read as the best day the line ever had; an average taken
/// over every call instead of the ones that produced an order flatters a line whose failures last
/// three seconds; and a page whose tiles vanish at zero reshuffles itself between one range and
/// the next.</summary>
public sealed class OrderDashboardTests
{
    private const int TenantId = 1;
    private static readonly Instant From = Instant.FromUnixTimeSeconds(1_754_000_000);
    private static readonly Instant To = From.Plus(Duration.FromDays(7));

    private static OrderCall Call(CallOutcome outcome, int? orderId, int duration)
        => OrderCall.Log(
            TenantId, null, "+994000000", orderId, outcome, duration, 4, 3, null, "url", null,
            From, "gemini-live", CallPipeline.GeminiLive_3_1, TokenUsage.Zero, 0m, From);

    private static Order Order(int customerId, int productId, decimal quantity, bool cancelled = false)
    {
        var order = Domain.Entities.Order.Place(TenantId, customerId, 1, null, null, From);
        order.AddLine(productId, quantity, From);
        if (cancelled)
        {
            order.Cancel(From);
        }

        return order;
    }

    private static OrderDashboardService Build(
        IReadOnlyList<OrderCall> calls, IReadOnlyList<Order> orders, IReadOnlyList<Product> products)
    {
        var uow = new Mock<IUnitOfWork>();

        var callRepo = new Mock<IOrderCallRepository>();
        callRepo.Setup(c => c.SearchAsync(From, To, null, default)).ReturnsAsync(calls);
        uow.SetupGet(u => u.OrderCalls).Returns(callRepo.Object);

        var orderRepo = new Mock<IOrderRepository>();
        orderRepo.Setup(o => o.GetAllAsync(default)).ReturnsAsync(orders);
        uow.SetupGet(u => u.Orders).Returns(orderRepo.Object);

        var productRepo = new Mock<IProductRepository>();
        productRepo.Setup(p => p.GetAllAsync(default)).ReturnsAsync(products);
        uow.SetupGet(u => u.Products).Returns(productRepo.Object);

        return new OrderDashboardService(uow.Object);
    }

    /// <summary>⚠ With an id. An entity built in memory has Id 0 until something saves it, so a
    /// catalogue of unsaved products looks to the service like one product numbered zero — every
    /// order line then matches nothing and the whole range values at zero.</summary>
    private static Product Water(decimal price)
        => WithId(Product.Create(TenantId, "Su 19L", 1, price, null, null, From), ProductId);

    private const int ProductId = 1;

    private static T WithId<T>(T entity, int id)
        where T : Domain.Abstractions.BaseEntity
    {
        typeof(Domain.Abstractions.BaseEntity)
            .GetProperty(nameof(Domain.Abstractions.BaseEntity.Id))!.SetValue(entity, id);
        return entity;
    }

    /// <summary>⚠ Not the same number as a resolved call. The agent can answer a question
    /// perfectly and take no order — a success by every measure except this one — so the page
    /// reports both rather than letting one stand for the other.</summary>
    [Fact]
    public async Task Calls_that_produced_an_order_are_counted_apart_from_calls_that_resolved()
    {
        var sut = Build(
            [
                Call(CallOutcome.ResolvedByAgent, orderId: 10, duration: 120),
                Call(CallOutcome.ResolvedByAgent, orderId: null, duration: 40),
                Call(CallOutcome.EscalatedAbandoned, orderId: null, duration: 20),
            ],
            [], []);

        var result = await sut.GetAsync(From, To, default);

        result.TotalCalls.ShouldBe(3);
        result.CallsWithOrder.ShouldBe(1);
        result.ByOutcome.Single(o => o.Outcome == CallOutcome.ResolvedByAgent).Calls.ShouldBe(2);
        result.OrderRate.ShouldBe(1.0 / 3);
    }

    /// <summary>Every outcome keeps its tile whether or not anything landed in it. A tile that
    /// disappears at zero moves the ones beside it, so the page changes shape between one range
    /// and the next and a reader loses the positions they had learned.</summary>
    [Fact]
    public async Task Every_outcome_is_listed_even_with_no_calls_at_all()
    {
        var result = await Build([], [], []).GetAsync(From, To, default);

        result.ByOutcome.Count.ShouldBe(Enum.GetValues<CallOutcome>().Length);
        result.ByOutcome.ShouldAllBe(o => o.Calls == 0 && o.Share == 0);

        // An average over no orders is not zero, and a rate over no calls is not 0%.
        result.AverageOrderValueAzn.ShouldBeNull();
        result.AverageOrderingCallDurationSeconds.ShouldBeNull();
        result.OrderRate.ShouldBeNull();
    }

    /// <summary>⚠ A cancelled order's value was never earned. Left in the total, a day of
    /// cancellations reads as the best day the line ever had.</summary>
    [Fact]
    public async Task Cancelled_orders_are_counted_but_never_valued()
    {
        var sut = Build(
            [],
            [Order(1, productId: ProductId, quantity: 2), Order(2, productId: ProductId, quantity: 5, cancelled: true)],
            [Water(3m)]);

        var result = await sut.GetAsync(From, To, default);

        result.OrdersPlaced.ShouldBe(1);
        result.OrdersCancelled.ShouldBe(1);
        result.TotalValueAzn.ShouldBe(6m);
        result.AverageOrderValueAzn.ShouldBe(6m);

        // And the cancelled order's two bidons are not demand for anything.
        result.ByProduct.Single().Quantity.ShouldBe(2m);
    }

    /// <summary>Over the calls that produced an order. A line whose failures last twenty seconds
    /// looks fast in a blended average, which is the opposite of what the number is for.</summary>
    [Fact]
    public async Task The_ordering_average_ignores_calls_that_took_no_order()
    {
        var sut = Build(
            [
                Call(CallOutcome.ResolvedByAgent, orderId: 10, duration: 200),
                Call(CallOutcome.ResolvedByAgent, orderId: 11, duration: 100),
                Call(CallOutcome.FailedAgentLimitation, orderId: null, duration: 6),
            ],
            [], []);

        var result = await sut.GetAsync(From, To, default);

        result.AverageOrderingCallDurationSeconds.ShouldBe(150);
        result.AverageCallDurationSeconds.ShouldBe(102);
        result.TotalDurationSeconds.ShouldBe(306);
    }

    /// <summary>Orders outside the range are not this range's business, however recently the
    /// tenant placed them — the repository has no range query, so the filtering happens here and
    /// is worth pinning.</summary>
    [Fact]
    public async Task Orders_placed_outside_the_range_are_left_out()
    {
        var older = Domain.Entities.Order.Place(TenantId, 1, 1, null, null, From.Minus(Duration.FromDays(30)));
        older.AddLine(ProductId, 4, From);

        var sut = Build([], [Order(1, productId: ProductId, quantity: 2), older], [Water(3m)]);

        var result = await sut.GetAsync(From, To, default);

        result.OrdersPlaced.ShouldBe(1);
        result.TotalValueAzn.ShouldBe(6m);
    }
}
