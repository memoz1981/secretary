using Secretary.Domain.Entities;
using Secretary.Domain.Enums;
using NodaTime;
using Shouldly;
using Xunit;

namespace Secretary.Domain.Tests.Entities;

public sealed class TenantTests
{
    private static readonly Instant Now = Instant.FromUtc(2026, 7, 11, 9, 0);

    [Fact]
    public void Create_sets_expected_defaults()
    {
        var tenant = Tenant.Create("Baku Barbershop", "Asia/Baku", "+994000000", showCallCosts: false, businessType: null, Now);

        tenant.Name.ShouldBe("Baku Barbershop");
        tenant.Timezone.ShouldBe("Asia/Baku");
        tenant.PhoneLine.ShouldBe("+994000000");
        tenant.Status.ShouldBe(EntityStatus.Active);
        tenant.CreatedAt.ShouldBe(Now);
        tenant.UpdatedAt.ShouldBe(Now);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Create_throws_when_name_is_blank(string? name)
    {
        Should.Throw<ArgumentException>(() => Tenant.Create(name!, "Asia/Baku", null, showCallCosts: false, businessType: null, Now));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Create_throws_when_timezone_is_blank(string? timezone)
    {
        Should.Throw<ArgumentException>(() => Tenant.Create("Baku Barbershop", timezone!, null, showCallCosts: false, businessType: null, Now));
    }

    [Fact]
    public void UpdateDetails_changes_name_timezone_and_phone_line()
    {
        var tenant = Tenant.Create("Baku Barbershop", "Asia/Baku", null, showCallCosts: false, businessType: null, Now);
        var later = Now + Duration.FromMinutes(1);

        tenant.UpdateDetails("Renamed", "Europe/London", "+44000000", showCallCosts: true, businessType: null, later);

        tenant.Name.ShouldBe("Renamed");
        tenant.Timezone.ShouldBe("Europe/London");
        tenant.PhoneLine.ShouldBe("+44000000");

        // ⚠ Whether a tenant may see what their calls cost is the platform's decision and moves
        // with the rest of their details. It defaults to off for everyone, because the margin
        // between what a call costs us and what they pay is readable the moment they know both.
        tenant.ShowCallCosts.ShouldBeTrue();
        tenant.UpdatedAt.ShouldBe(later);
    }

    [Fact]
    public void Deactivate_then_Reactivate_round_trips_status()
    {
        var tenant = Tenant.Create("Baku Barbershop", "Asia/Baku", null, showCallCosts: false, businessType: null, Now);

        tenant.Deactivate(Now);
        tenant.Status.ShouldBe(EntityStatus.Inactive);

        tenant.Reactivate(Now);
        tenant.Status.ShouldBe(EntityStatus.Active);
    }

    /// <summary>⚠ C# casts any int to an enum without complaint, so a client posting a number
    /// nobody defined stores that number, and every screen reading it back falls through its
    /// lookup to a blank cell. The tenant list would then show a business whose type is neither
    /// set nor unset nor any of the options.</summary>
    [Fact]
    public void A_business_type_nobody_defined_is_refused()
        => Should.Throw<ArgumentException>(() => Tenant.Create(
            "Baku Barbershop", "Asia/Baku", null, showCallCosts: false, (BusinessType)47, Now));

    /// <summary>Not set is a real state and stays available. The accounts that existed before the
    /// column did are genuinely unknown, and a guess in this field is worse than a blank.</summary>
    [Fact]
    public void No_business_type_is_allowed()
        => Tenant.Create("Baku Barbershop", "Asia/Baku", null, showCallCosts: false, businessType: null, Now)
            .BusinessType.ShouldBeNull();

    [Fact]
    public void The_business_type_can_be_set_later()
    {
        var tenant = Tenant.Create("Baku Barbershop", "Asia/Baku", null, showCallCosts: false, businessType: null, Now);

        tenant.UpdateDetails("Baku Barbershop", "Asia/Baku", null, showCallCosts: false, BusinessType.Barbershop, Now);

        tenant.BusinessType.ShouldBe(BusinessType.Barbershop);
    }
}
