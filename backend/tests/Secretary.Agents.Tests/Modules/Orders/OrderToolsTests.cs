using System.Text.Json;
using Secretary.Agents.Orders;
using Secretary.Agents.ServiceCatalog;
using Secretary.Agents.Tools;
using Secretary.Application.Abstractions;
using Secretary.Application.Abstractions.Persistence;
using Secretary.Application.Services;
using Secretary.Domain.Abstractions;
using Secretary.Domain.Entities;
using Secretary.Domain.Enums;
using Secretary.Domain.ValueObjects;
using Secretary.Application.Pricing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using NodaTime;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace Secretary.Agents.Tests;

/// <summary>The guards that stop a one-shot order going wrong quietly.
///
/// The order used to be assembled a product at a time, and each AddToOrder call checked the cap
/// and echoed the basket back. One call replaced all of that, so everything those round trips
/// caught has to be caught here instead — before a row exists, because after it exists the
/// caller has already been told a number.</summary>
public sealed class OrderToolsTests
{
    private static readonly Instant Now = Instant.FromUnixTimeSeconds(1_754_000_000);

    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<ICustomerRepository> _customers = new();
    private readonly Mock<IProductRepository> _products = new();
    private readonly Mock<IMeasurementUnitRepository> _units = new();
    private readonly Mock<IOrderRepository> _orders = new();
    private readonly Mock<IOrderSettingsRepository> _settings = new();
    private readonly Mock<IBusinessHoursRepository> _hours = new();
    private readonly Mock<IOrderCallRepository> _orderCallRepo = new();
    private readonly OrderCallSession _session = new();

    /// <summary>Movable, so a test can prove the delivery day is worked out per call rather than
    /// cached with the policy behind it.</summary>
    private Instant _now = Now;

    private readonly OrderTools _sut;
    private readonly EscalationTools _escalation;
    private readonly OrderCallService _orderCalls;

    private readonly ITestOutputHelper _output;

    public OrderToolsTests(ITestOutputHelper output)
    {
        _output = output;

        _uow.SetupGet(u => u.Customers).Returns(_customers.Object);
        _uow.SetupGet(u => u.Products).Returns(_products.Object);
        _uow.SetupGet(u => u.Units).Returns(_units.Object);
        _uow.SetupGet(u => u.Orders).Returns(_orders.Object);
        _uow.SetupGet(u => u.OrderSettings).Returns(_settings.Object);
        _uow.SetupGet(u => u.BusinessHours).Returns(_hours.Object);
        _uow.SetupGet(u => u.OrderCalls).Returns(_orderCallRepo.Object);
        _uow.SetupGet(u => u.Clients).Returns(new Mock<IClientRepository>().Object);
        _uow.SetupGet(u => u.Escalations).Returns(new Mock<IEscalationRepository>().Object);

        var clock = new Mock<IClock>();
        clock.Setup(c => c.GetCurrentInstant()).Returns(() => _now);

        var tenant = new Mock<ICurrentTenantProvider>();
        tenant.SetupGet(t => t.TenantId).Returns(1);

        // Open every day, so the delivery day is only ever the lead time away and a test that is
        // not about the calendar does not have to think about one.
        _hours.Setup(h => h.GetWeekAsync(It.IsAny<CancellationToken>())).ReturnsAsync(
            Enum.GetValues<IsoDayOfWeek>()
                .Where(d => d != IsoDayOfWeek.None)
                .Select(d => BusinessHours.Open(1, d, new LocalTime(9, 0), new LocalTime(21, 0), Now))
                .ToList());

        var notifier = new NullAgentDirectoryChangeNotifier();
        var businessHours = new BusinessHoursService(
            _hours.Object, _uow.Object, clock.Object, tenant.Object, notifier);

        var identity = new CustomerIdentityService(_uow.Object, clock.Object, tenant.Object);
        var orderService = new OrderService(_uow.Object, businessHours, clock.Object, tenant.Object, notifier);
        var clients = new ClientService(_uow.Object, clock.Object, tenant.Object);
        _escalation = new EscalationTools(
            new EscalationService(_uow.Object, clock.Object, tenant.Object, clients));

        // The directory's store is process-wide, so one test's catalogue would otherwise be
        // served to the next — every test here is tenant 1.
        OrderDirectoryStore.Invalidate(1);
        var directory = new TenantOrderDirectory(orderService, businessHours, clock.Object, tenant.Object);

        _orderCalls = new OrderCallService(
            _uow.Object, clock.Object, tenant.Object,
            new TokenPricebook(Options.Create(new ModelPricingOptions())));

        _sut = new OrderTools(
            identity, orderService, directory, _session, _escalation, NullLogger<OrderTools>.Instance);
    }

    /// <summary>The instruction file names these tools, so a rename here silently breaks it.
    /// Nine, down from eleven — the count moved little, the shape moved a lot.</summary>
    [Fact]
    public void The_module_exposes_its_tools_under_the_names_the_instructions_use()
    {
        var tools = BuildModule().BuildTools();

        tools.OfType<Microsoft.Extensions.AI.AIFunction>().Select(f => f.Name).ShouldBe(
            new[]
            {
                nameof(OrderTools.FindCustomerById),
                nameof(OrderTools.FindCustomerByPhone),
                nameof(OrderTools.FindCustomerByAddress),
                nameof(OrderTools.RegisterCustomer),
                nameof(OrderTools.ListProducts),
                nameof(OrderTools.PlaceOrder),
                nameof(OrderTools.CancelOrder),
                nameof(EscalationTools.EscalateToHuman),
                nameof(CallControlTools.EndCall),
            },
            ignoreOrder: false);
    }

    /// <summary>Two bidons and then one more is three, and three is over a cap of two. Checking
    /// each line on its own would let a caller past the limit by asking twice — which is exactly
    /// what the per-call check used to prevent.</summary>
    [Fact]
    public async Task The_cap_is_judged_on_the_whole_order_not_one_line()
    {
        GivenCatalog(WithId(Product.Create(1, "Sirab 19L", 1, 4.50m, null, maxOrderQuantity: 2m, Now), 7));

        var result = await _sut.PlaceOrder(11, 0, [new OrderLineInput(7, 2m), new OrderLineInput(7, 1m)]);

        result.ShouldStartWith("OVER_MAXIMUM.");

        // A maximum is a quantity of something, so it carries the unit too.
        result.ShouldContain("2 ədəd");
        result.ShouldContain("Sirab 19L");
        _orders.Verify(o => o.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>The model once passed 43 as an address id — the mənzil out of "ev 12, mənzil 43".
    /// An id that is not this customer's must cost a retry, never a delivery to a stranger.</summary>
    [Fact]
    public async Task An_address_that_is_not_this_customers_is_refused_and_theirs_are_listed()
    {
        GivenCatalog(WithId(Product.Create(1, "Sirab 19L", 1, 4.50m, null, null, Now), 7));
        GivenAddresses(11, WithId(Address(11, "Xətai", "Sarayevo", "12"), 87));

        var result = await _sut.PlaceOrder(11, 43, [new OrderLineInput(7, 2m)]);

        result.ShouldStartWith("NO_SUCH_ADDRESS.");
        result.ShouldContain("87");
        _orders.Verify(o => o.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>A caller with one address never has to choose, so the agent is not asked for an
    /// id it would have to invent.</summary>
    [Fact]
    public async Task A_single_address_is_used_without_being_asked_for()
    {
        GivenCatalog(WithId(Product.Create(1, "Sirab 19L", 1, 4.50m, null, null, Now), 7));
        GivenAddresses(11, WithId(Address(11, "Xətai", "Sarayevo", "12"), 87));
        GivenOrdersSave(orderId: 42);

        var result = await _sut.PlaceOrder(11, 0, [new OrderLineInput(7, 5m)]);

        result.ShouldStartWith("ORDER_PLACED. Sifariş 42:");

        // The readback is built from what went into the database, not from what the model
        // remembers saying — that is the reason the order is one call.
        //
        // Quantity, unit, product — the way it is said. It used to read "Sirab 19L × 5" and the
        // agent said the multiplication sign out loud: "Sirab vuraq beş".
        result.ShouldContain("5 ədəd Sirab 19L");
        result.ShouldNotContain("×");
        result.ShouldContain("Çatdırılma:");

        // Never a year. A caller was once offered, and accepted, the 31st of December 2031.
        result.ShouldNotContain("202");
    }

    [Fact]
    public async Task A_product_id_that_is_not_in_the_catalogue_lists_what_is()
    {
        GivenCatalog(WithId(Product.Create(1, "Sirab 19L", 1, 4.50m, null, null, Now), 7));

        var result = await _sut.PlaceOrder(11, 0, [new OrderLineInput(999, 1m)]);

        result.ShouldStartWith("NO_SUCH_PRODUCT.");
        result.ShouldContain("7 Sirab 19L");
    }

    /// <summary>Order numbers are no more secret than customer numbers. Cancelling is limited to
    /// the order this call placed, so guessing a number cannot cancel a stranger's delivery.</summary>
    [Fact]
    public async Task An_order_from_another_call_cannot_be_cancelled()
    {
        var result = await _sut.CancelOrder(orderId: 500, customerId: 11);

        result.ShouldBe("NOT_THIS_CALL.");
        _orders.Verify(o => o.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task The_order_just_placed_can_be_cancelled_once()
    {
        GivenCatalog(WithId(Product.Create(1, "Sirab 19L", 1, 4.50m, null, null, Now), 7));
        GivenAddresses(11, WithId(Address(11, "Xətai", "Sarayevo", "12"), 87));
        var placed = GivenOrdersSave(orderId: 42);

        await _sut.PlaceOrder(11, 0, [new OrderLineInput(7, 1m)]);
        _orders.Setup(o => o.GetByIdAsync(42, It.IsAny<CancellationToken>())).ReturnsAsync(placed);

        (await _sut.CancelOrder(42, 11)).ShouldBe("ORDER_CANCELLED. Sifariş 42");

        // Twice would throw out of the domain and read to the model as something to escalate.
        (await _sut.CancelOrder(42, 11)).ShouldBe("NOT_THIS_CALL.");
    }

    /// <summary>Every lookup answers in the same three markers, which is what let the instruction
    /// file describe identity once instead of once per route.</summary>
    [Fact]
    public async Task A_lookup_returns_the_name_and_the_address_for_the_agent_to_read_back()
    {
        var customer = WithId(Customer.Create(1, "Elvin Məmmədov", Now), 11);
        _customers.Setup(c => c.GetByIdAsync(11, It.IsAny<CancellationToken>())).ReturnsAsync(customer);
        GivenAddresses(11, WithId(Address(11, "Xətai", "Sarayevo", "12"), 87));

        var result = await _sut.FindCustomerById(11);

        result.ShouldStartWith("FOUND. Müştəri 11, Elvin Məmmədov.");
        result.ShouldContain("Xətai");
        result.ShouldContain("Sarayevo");
    }

    /// <summary>A delivery business cannot use a customer it holds no address for, so the lookup
    /// says not found and registration picks them up — where the phone check reunites them with
    /// this same record rather than making a second one.</summary>
    [Fact]
    public async Task A_customer_with_no_address_is_not_a_match()
    {
        _customers
            .Setup(c => c.GetByIdAsync(11, It.IsAny<CancellationToken>()))
            .ReturnsAsync(WithId(Customer.Create(1, "Elvin Məmmədov", Now), 11));

        GivenAddresses(11);

        (await _sut.FindCustomerById(11)).ShouldBe("NOT_FOUND.");
    }

    /// <summary>The order arrives as an array of objects, which is the one argument shape in this
    /// module that has more structure than a scalar — and a realtime model once sent a tool call
    /// whose JSON arrived truncated mid-string. Pin the schema the model is handed: a productId
    /// and a quantity per line, so a truncated call fails to parse rather than placing half an
    /// order.</summary>
    [Fact]
    public void The_order_lines_reach_the_model_as_an_array_of_id_and_quantity()
    {
        var placeOrder = BuildModule()
            .BuildTools()
            .OfType<Microsoft.Extensions.AI.AIFunction>()
            .Single(f => f.Name == nameof(OrderTools.PlaceOrder));

        var lines = placeOrder.JsonSchema.GetProperty("properties").GetProperty("lines");
        lines.GetProperty("type").GetString().ShouldBe("array");

        var line = lines.GetProperty("items").GetProperty("properties");
        line.TryGetProperty("productId", out _).ShouldBeTrue();
        line.TryGetProperty("quantity", out _).ShouldBeTrue();
    }

    /// <summary>These reads happen inside a tool call, which happens in the middle of a spoken
    /// sentence. The caller hears the round trip, so it is worth not making it twice.</summary>
    [Fact]
    public async Task The_catalogue_is_read_once_and_then_held()
    {
        GivenCatalog(WithId(Product.Create(1, "Sirab 19L", 1, 4.50m, null, null, Now), 7));

        await _sut.ListProducts();
        await _sut.ListProducts();

        _products.Verify(p => p.GetCatalogAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Through the real notifier, not the store — a product withdrawn on the Products
    /// page and still in the cache is one the agent goes on taking orders for.</summary>
    [Fact]
    public async Task Changing_the_catalogue_is_picked_up_on_the_next_call()
    {
        GivenCatalog(WithId(Product.Create(1, "Sirab 19L", 1, 4.50m, null, null, Now), 7));
        await _sut.ListProducts();

        new AgentDirectoryChangeNotifier().NotifyChanged(1);
        await _sut.ListProducts();

        _products.Verify(p => p.GetCatalogAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    /// <summary>The policy is what holds still. The day it produces moves at midnight, and a
    /// cached one would have the agent promising yesterday.</summary>
    [Fact]
    public async Task The_delivery_day_moves_with_the_date_even_though_the_policy_is_cached()
    {
        GivenCatalog(WithId(Product.Create(1, "Sirab 19L", 1, 4.50m, null, null, Now), 7));
        GivenAddresses(11, WithId(Address(11, "Xətai", "Sarayevo", "12"), 87));
        GivenOrdersSave(orderId: 42);

        var today = await _sut.PlaceOrder(11, 0, [new OrderLineInput(7, 1m)]);

        _now = Now.Plus(Duration.FromDays(1));
        _session.Cancelled();
        var tomorrow = await _sut.PlaceOrder(11, 0, [new OrderLineInput(7, 1m)]);

        tomorrow.ShouldNotBe(today);

        // Read once all the same — the settings and the working week did not change.
        _settings.Verify(
            s => s.GetForCurrentTenantAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>The row lands in ord.Calls carrying the two things only this module could know.
    /// A browser call has no number to look anyone up by afterwards, so if the session does not
    /// remember who was identified, the log says nothing about who called.</summary>
    [Fact]
    public async Task The_logged_call_carries_the_customer_and_the_order_it_produced()
    {
        GivenCatalog(WithId(Product.Create(1, "Sirab 19L", 1, 4.50m, null, null, Now), 7));
        GivenAddresses(11, WithId(Address(11, "Xətai", "Sarayevo", "12"), 87));
        GivenOrdersSave(orderId: 42);
        await _sut.PlaceOrder(11, 0, [new OrderLineInput(7, 3m)]);

        OrderCall? logged = null;
        _orderCallRepo
            .Setup(r => r.AddAsync(It.IsAny<OrderCall>(), It.IsAny<CancellationToken>()))
            .Callback<OrderCall, CancellationToken>((call, _) => logged = call);

        await BuildModule().LogCallAsync(
            new CallLogEntry(
                "local-device-call", CallClassification.InquiryOther, CallOutcome.ResolvedByAgent,
                DurationSeconds: 39, TurnCount: 7, CallerTurnCount: 6, "no recording", Transcript: null,
                Now, CallPipeline.GeminiLive_3_1, [new ModelUsage("gemini-3.1-flash-live-preview", TokenUsage.Zero)]),
            default);

        logged.ShouldNotBeNull();
        logged!.CustomerId.ShouldBe(11);
        logged.RelatedOrderId.ShouldBe(42);
        logged.DurationSeconds.ShouldBe(39);
        logged.AgentModel.ShouldBe("gemini-3.1-flash-live-preview");
    }

    /// <summary>A call that never identified anyone still gets a row — the caller identifier
    /// stands in, and a page full of those is how you find out identification is failing.</summary>
    [Fact]
    public async Task A_call_that_identified_nobody_is_still_logged()
    {
        OrderCall? logged = null;
        _orderCallRepo
            .Setup(r => r.AddAsync(It.IsAny<OrderCall>(), It.IsAny<CancellationToken>()))
            .Callback<OrderCall, CancellationToken>((call, _) => logged = call);

        await BuildModule().LogCallAsync(
            new CallLogEntry(
                "local-device-call", CallClassification.InquiryOther, CallOutcome.FailedAgentLimitation,
                DurationSeconds: 8, TurnCount: 1, CallerTurnCount: 1, "no recording", Transcript: null,
                Now, CallPipeline.GeminiLive_3_1, [new ModelUsage("gemini-3.1-flash-live-preview", TokenUsage.Zero)]),
            default);

        logged.ShouldNotBeNull();
        logged!.CustomerId.ShouldBeNull();
        logged.RelatedOrderId.ShouldBeNull();
        logged.CallerPhoneNumber.ShouldBe("local-device-call");
    }

    /// <summary>What the tool declarations cost, in characters, every time they are sent.
    ///
    /// Here because of a measurement the log could not explain: on a real Gemini call the prompt
    /// jumped by ~2,200 text tokens on the turn that made the first tool call, held for the rest
    /// of the call, and vanished on the last one. The schemas are the only thing on the wire the
    /// right size to account for that.
    ///
    /// The number is a ceiling, not a target. It is here so that adding a tool, or a paragraph
    /// to a description, shows up as a failing test rather than as a bill.</summary>
    [Fact]
    public void The_toolset_schema_stays_within_its_budget()
    {
        var total = 0;
        foreach (var tool in BuildModule().BuildTools().OfType<Microsoft.Extensions.AI.AIFunction>())
        {
            var declaration = JsonSerializer.Serialize(
                new { name = tool.Name, description = tool.Description, parameters = tool.JsonSchema });

            _output.WriteLine($"{tool.Name,-24} {declaration.Length,6} chars");
            total += declaration.Length;
        }

        _output.WriteLine($"{"TOTAL",-24} {total,6} chars");

        // Roughly four characters to a token. Raise this deliberately, with a reason, or trim a
        // description instead.
        total.ShouldBeLessThan(6000);
    }

    private OrdersAgentModule BuildModule()
        => new(_sut, _escalation, new CallControlTools(), _session, _orderCalls);

    private void GivenCatalog(params Product[] products)
    {
        _products.Setup(p => p.GetCatalogAsync(It.IsAny<CancellationToken>())).ReturnsAsync(products);
        _units
            .Setup(u => u.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([WithId(MeasurementUnit.Create("ədəd", Now), 1)]);
    }

    private void GivenAddresses(int customerId, params CustomerAddress[] addresses)
        => _customers
            .Setup(c => c.GetAddressesAsync(customerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(addresses);

    /// <summary>The identity is assigned by the insert, so the fake save has to do what the
    /// database would — otherwise the order comes back with id 0 and PlaceAsync throws.</summary>
    private Order GivenOrdersSave(int orderId)
    {
        Order? captured = null;
        _orders
            .Setup(o => o.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
            .Callback<Order, CancellationToken>((order, _) => captured = order);

        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1)
            .Callback(() =>
            {
                if (captured is not null)
                {
                    WithId(captured, orderId);
                }
            });

        return Order.Place(1, 11, 87, null, null, Now);
    }

    private static CustomerAddress Address(int customerId, string district, string street, string building)
        => CustomerAddress.Create(
            customerId, district, null, street, null, building, null, null, null, null, isDefault: true, Now);

    /// <summary>Identity ids are 0 until a real save; force one where a test needs entities to
    /// be told apart. Same idiom as ClientServiceTests.</summary>
    private static T WithId<T>(T entity, int id)
        where T : BaseEntity
    {
        typeof(BaseEntity).GetProperty(nameof(BaseEntity.Id))!.SetValue(entity, id);
        return entity;
    }
}
