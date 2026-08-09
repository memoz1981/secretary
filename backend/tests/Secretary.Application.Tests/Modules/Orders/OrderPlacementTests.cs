using Secretary.Application.Abstractions;
using Secretary.Application.Abstractions.Persistence;
using Secretary.Application.Services;
using Secretary.Domain.Entities;
using Moq;
using NodaTime;
using Shouldly;
using Xunit;

namespace Secretary.Application.Tests;

/// <summary>An order that was not saved must never come back looking like one that was. The
/// appointment line has confirmed bookings it never made three times; here the confirmation
/// string cannot be produced without a row behind it.</summary>
public sealed class OrderPlacementTests
{
    private static readonly Instant Now = Instant.FromUnixTimeSeconds(1_754_000_000);

    private static OrderService Build(Mock<IUnitOfWork> uow)
    {
        var clock = new Mock<IClock>();
        clock.Setup(c => c.GetCurrentInstant()).Returns(Now);

        var tenant = new Mock<ICurrentTenantProvider>();
        tenant.SetupGet(t => t.TenantId).Returns(1);

        var hours = new BusinessHoursService(
            new Mock<IBusinessHoursRepository>().Object, uow.Object, clock.Object, tenant.Object);

        return new OrderService(uow.Object, hours, clock.Object, tenant.Object);
    }

    /// <summary>The identity comes from the insert, so an unsaved order still has id 0. Returning
    /// it would let the tool say "sifarişiniz qeydə alındı" about nothing at all.</summary>
    [Fact]
    public async Task An_order_that_never_got_an_id_throws_rather_than_returning()
    {
        var uow = new Mock<IUnitOfWork>();
        uow.SetupGet(u => u.Orders).Returns(new Mock<IOrderRepository>().Object);
        uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(0);

        var service = Build(uow);

        var failure = await Should.ThrowAsync<InvalidOperationException>(
            () => service.PlaceAsync(1, 1, [(ProductId: 1, Quantity: 3m)], null, null, default));

        failure.Message.ShouldContain("order number");
    }

    [Fact]
    public async Task An_order_with_no_lines_is_refused_before_anything_is_saved()
    {
        var uow = new Mock<IUnitOfWork>();
        var service = Build(uow);

        await Should.ThrowAsync<InvalidOperationException>(
            () => service.PlaceAsync(1, 1, [], null, null, default));

        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>"İki bidon, bir də bir bidon" is three, not two lines.</summary>
    [Fact]
    public void Asking_for_the_same_product_twice_adds_up()
    {
        var order = Order.Place(1, 1, 1, null, null, Now);

        order.AddLine(7, 2m, Now);
        order.AddLine(7, 1m, Now);

        order.Lines.Count.ShouldBe(1);
        order.Lines[0].Quantity.ShouldBe(3m);
    }

    [Fact]
    public void A_quantity_of_zero_or_less_is_not_an_order_line()
    {
        var order = Order.Place(1, 1, 1, null, null, Now);

        Should.Throw<ArgumentOutOfRangeException>(() => order.AddLine(7, 0m, Now));
        Should.Throw<ArgumentOutOfRangeException>(() => order.AddLine(7, -1m, Now));
    }
}
