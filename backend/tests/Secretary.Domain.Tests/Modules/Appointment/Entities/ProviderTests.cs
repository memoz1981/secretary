using Secretary.Domain.Entities;
using Secretary.Domain.Enums;
using NodaTime;
using Shouldly;
using Xunit;

namespace Secretary.Domain.Tests.Entities;

public sealed class ProviderTests
{
    private const int TenantId = 1;
    private static readonly Instant Now = Instant.FromUtc(2026, 7, 11, 9, 0);

    [Fact]
    public void Create_sets_tenant_and_name()
    {
        var provider = Provider.Create(TenantId, "Rasim (chair 1)", Now);

        provider.TenantId.ShouldBe(TenantId);
        provider.Name.ShouldBe("Rasim (chair 1)");
        provider.Status.ShouldBe(EntityStatus.Active);
        provider.CreatedAt.ShouldBe(Now);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Create_throws_when_name_is_blank(string? name)
    {
        Should.Throw<ArgumentException>(() => Provider.Create(TenantId, name!, Now));
    }

    [Fact]
    public void UpdateDetails_changes_name()
    {
        var provider = Provider.Create(TenantId, "Rasim (chair 1)", Now);
        var later = Now + Duration.FromMinutes(1);

        provider.UpdateDetails("Rasim (chair 2)", later);

        provider.Name.ShouldBe("Rasim (chair 2)");
        provider.UpdatedAt.ShouldBe(later);
    }

    [Fact]
    public void Deactivate_then_Reactivate_round_trips_status()
    {
        var provider = Provider.Create(TenantId, "Rasim (chair 1)", Now);

        provider.Deactivate(Now);
        provider.Status.ShouldBe(EntityStatus.Inactive);

        provider.Reactivate(Now);
        provider.Status.ShouldBe(EntityStatus.Active);
    }
}
