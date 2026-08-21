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
        var tenant = Tenant.Create("Baku Barbershop", "Asia/Baku", "+994000000", showCallCosts: false, Now);

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
        Should.Throw<ArgumentException>(() => Tenant.Create(name!, "Asia/Baku", null, showCallCosts: false, Now));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Create_throws_when_timezone_is_blank(string? timezone)
    {
        Should.Throw<ArgumentException>(() => Tenant.Create("Baku Barbershop", timezone!, null, showCallCosts: false, Now));
    }

    [Fact]
    public void UpdateDetails_changes_name_timezone_and_phone_line()
    {
        var tenant = Tenant.Create("Baku Barbershop", "Asia/Baku", null, showCallCosts: false, Now);
        var later = Now + Duration.FromMinutes(1);

        tenant.UpdateDetails("Renamed", "Europe/London", "+44000000", showCallCosts: true, later);

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
        var tenant = Tenant.Create("Baku Barbershop", "Asia/Baku", null, showCallCosts: false, Now);

        tenant.Deactivate(Now);
        tenant.Status.ShouldBe(EntityStatus.Inactive);

        tenant.Reactivate(Now);
        tenant.Status.ShouldBe(EntityStatus.Active);
    }
}
