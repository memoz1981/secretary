using Secretary.Application.Abstractions;
using Secretary.Application.Abstractions.Persistence;
using Secretary.Application.Services;
using Secretary.Domain.Entities;
using Moq;
using NodaTime;
using Shouldly;
using Xunit;

namespace Secretary.Application.Tests;

/// <summary>Who may be shown what a call cost to run.
///
/// ⚠ The figure is our own margin seen from the other side: a tenant who knows what they pay and
/// what the call cost knows what we make. So it is off for everybody by default, it is the
/// platform's switch and not the tenant's, and it is asked here — where the response is built —
/// rather than in a page that simply declines to draw it. A number absent from the screen and
/// present in the JSON is hidden from nobody.</summary>
public sealed class CallCostVisibilityTests
{
    private static readonly Instant Now = Instant.FromUnixTimeSeconds(1_754_000_000);

    private static CallCostVisibility For(int? tenantId, Tenant? tenant)
    {
        var tenants = new Mock<ITenantRepository>();
        tenants.Setup(t => t.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(tenant);

        var uow = new Mock<IUnitOfWork>();
        uow.SetupGet(u => u.Tenants).Returns(tenants.Object);

        var current = new Mock<ICurrentTenantProvider>();
        current.SetupGet(c => c.TenantId).Returns(tenantId);

        return new CallCostVisibility(uow.Object, current.Object);
    }

    private static Tenant Tenant(bool showCallCosts)
        => Secretary.Domain.Entities.Tenant.Create("Salon Demo", "Asia/Baku", null, showCallCosts, Now);

    /// <summary>The platform admin has no tenant of their own, and that absence is the signal.</summary>
    [Fact]
    public async Task A_caller_with_no_tenant_is_us_and_sees_everything()
        => (await For(tenantId: null, tenant: null).IsAllowedAsync(default)).ShouldBeTrue();

    [Fact]
    public async Task A_tenant_the_platform_has_switched_on_sees_costs()
        => (await For(1, Tenant(showCallCosts: true)).IsAllowedAsync(default)).ShouldBeTrue();

    [Fact]
    public async Task A_tenant_by_default_does_not()
        => (await For(1, Tenant(showCallCosts: false)).IsAllowedAsync(default)).ShouldBeFalse();

    /// <summary>⚠ Fails closed. A lookup that returns nothing is a question we could not answer,
    /// and the answer to "may they see our margin" is not "probably".</summary>
    [Fact]
    public async Task A_tenant_that_cannot_be_read_sees_nothing()
        => (await For(1, tenant: null).IsAllowedAsync(default)).ShouldBeFalse();

    /// <summary>⚠ Null, never zero. A zero is a claim that the call was free — it would be summed
    /// into a total, divided into an average and drawn on a chart as a real figure.</summary>
    [Fact]
    public async Task A_hidden_cost_is_absent_rather_than_free()
        => (await For(1, Tenant(showCallCosts: false)).ShowAsync(0.07m, default)).ShouldBeNull();

    [Fact]
    public async Task A_visible_cost_comes_through_untouched()
        => (await For(1, Tenant(showCallCosts: true)).ShowAsync(0.07m, default)).ShouldBe(0.07m);

    /// <summary>One question per request, however many rows a page has. The answer cannot change
    /// while one response is being built, and a call log of fifty rows would otherwise ask the
    /// database fifty times.</summary>
    [Fact]
    public async Task The_answer_is_looked_up_once()
    {
        var tenants = new Mock<ITenantRepository>();
        tenants.Setup(t => t.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Tenant(showCallCosts: true));

        var uow = new Mock<IUnitOfWork>();
        uow.SetupGet(u => u.Tenants).Returns(tenants.Object);

        var current = new Mock<ICurrentTenantProvider>();
        current.SetupGet(c => c.TenantId).Returns(1);

        var visibility = new CallCostVisibility(uow.Object, current.Object);
        await visibility.IsAllowedAsync(default);
        await visibility.IsAllowedAsync(default);
        await visibility.ShowAsync(0.01m, default);

        tenants.Verify(t => t.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
