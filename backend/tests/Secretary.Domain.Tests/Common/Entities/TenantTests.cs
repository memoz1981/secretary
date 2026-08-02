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
        var tenant = Tenant.Create("Baku Barbershop", "Asia/Baku", "+994000000", Now);

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
        Should.Throw<ArgumentException>(() => Tenant.Create(name!, "Asia/Baku", null, Now));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Create_throws_when_timezone_is_blank(string? timezone)
    {
        Should.Throw<ArgumentException>(() => Tenant.Create("Baku Barbershop", timezone!, null, Now));
    }

    [Fact]
    public void UpdateDetails_changes_name_timezone_and_phone_line()
    {
        var tenant = Tenant.Create("Baku Barbershop", "Asia/Baku", null, Now);
        var later = Now + Duration.FromMinutes(1);

        tenant.UpdateDetails("Renamed", "Europe/London", "+44000000", later);

        tenant.Name.ShouldBe("Renamed");
        tenant.Timezone.ShouldBe("Europe/London");
        tenant.PhoneLine.ShouldBe("+44000000");
        tenant.UpdatedAt.ShouldBe(later);
    }

    [Fact]
    public void Deactivate_then_Reactivate_round_trips_status()
    {
        var tenant = Tenant.Create("Baku Barbershop", "Asia/Baku", null, Now);

        tenant.Deactivate(Now);
        tenant.Status.ShouldBe(EntityStatus.Inactive);

        tenant.Reactivate(Now);
        tenant.Status.ShouldBe(EntityStatus.Active);
    }
}
