using Secretary.Domain.Entities;
using Secretary.Domain.Enums;
using NodaTime;
using Shouldly;
using Xunit;

namespace Secretary.Domain.Tests.Entities;

public sealed class ProviderServiceOfferingTests
{
    private static readonly Instant Now = Instant.FromUtc(2026, 7, 11, 9, 0);

    [Fact]
    public void Create_starts_active_with_expected_links()
    {
        var assignment = ProviderServiceOffering.Create(1, 20, 30, Now);

        assignment.TenantId.ShouldBe(1);
        assignment.ProviderId.ShouldBe(20);
        assignment.ServiceOfferingId.ShouldBe(30);
        assignment.Status.ShouldBe(EntityStatus.Active);
        assignment.CreatedAt.ShouldBe(Now);
    }

    [Fact]
    public void Deactivate_then_Reactivate_round_trips_the_checkbox()
    {
        var assignment = ProviderServiceOffering.Create(1, 20, 30, Now);

        assignment.Deactivate(Now);
        assignment.Status.ShouldBe(EntityStatus.Inactive);

        assignment.Reactivate(Now);
        assignment.Status.ShouldBe(EntityStatus.Active);
    }
}
