using Secretary.Domain.Entities;
using NodaTime;
using Shouldly;
using Xunit;

namespace Secretary.Domain.Tests.Entities;

public sealed class ServiceOfferingTests
{
    private const int TenantId = 1;
    private static readonly Instant Now = Instant.FromUtc(2026, 7, 11, 9, 0);

    [Fact]
    public void Create_sets_expected_fields()
    {
        var service = ServiceOffering.Create(TenantId, "Haircut", 15m, 30, Now);

        service.Name.ShouldBe("Haircut");
        service.Price.ShouldBe(15m);
        service.DurationMinutes.ShouldBe(30);
        service.UpdatedAt.ShouldBe(Now);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Create_throws_when_name_is_blank(string name)
    {
        Should.Throw<ArgumentException>(() => ServiceOffering.Create(TenantId, name, 15m, 30, Now));
    }

    [Fact]
    public void Create_throws_when_price_is_negative()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => ServiceOffering.Create(TenantId, "Haircut", -1m, 30, Now));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Create_throws_when_duration_is_not_positive(int duration)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => ServiceOffering.Create(TenantId, "Haircut", 15m, duration, Now));
    }

    [Fact]
    public void UpdateDetails_bumps_updated_at()
    {
        var service = ServiceOffering.Create(TenantId, "Haircut", 15m, 30, Now);
        var later = Now + Duration.FromMinutes(5);

        service.UpdateDetails("Haircut deluxe", 20m, 40, later);

        service.Name.ShouldBe("Haircut deluxe");
        service.Price.ShouldBe(20m);
        service.DurationMinutes.ShouldBe(40);
        service.UpdatedAt.ShouldBe(later);
    }
}
