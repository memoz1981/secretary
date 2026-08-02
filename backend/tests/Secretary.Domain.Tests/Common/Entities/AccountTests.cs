using Secretary.Domain.Entities;
using Secretary.Domain.Enums;
using NodaTime;
using Shouldly;
using Xunit;

namespace Secretary.Domain.Tests.Entities;

public sealed class AccountTests
{
    private static readonly Instant Now = Instant.FromUtc(2026, 7, 11, 9, 0);
    private const int TenantId = 1;

    [Fact]
    public void CreatePlatformAdmin_has_no_tenant_and_is_active()
    {
        var account = Account.CreatePlatformAdmin("Product Owner", "owner@platform.com", "hash", Now);

        account.TenantId.ShouldBeNull();
        account.Role.ShouldBe(AccountRole.PlatformAdmin);
        account.Status.ShouldBe(EntityStatus.Active);
    }

    [Fact]
    public void CreateOwner_is_tenant_scoped_and_active()
    {
        var account = Account.CreateOwner(TenantId, "Elvin", "elvin@business.az", "hash", Now);

        account.TenantId.ShouldBe(TenantId);
        account.Role.ShouldBe(AccountRole.Owner);
        account.Status.ShouldBe(EntityStatus.Active);
    }

    [Fact]
    public void AddStaff_is_tenant_scoped_and_active_immediately()
    {
        var account = Account.AddStaff(TenantId, "Rasim", "rasim@business.az", "hash", Now);

        account.Role.ShouldBe(AccountRole.Staff);
        account.Status.ShouldBe(EntityStatus.Active);
    }

    [Fact]
    public void CreateAgent_is_tenant_scoped_role_agent_and_active()
    {
        var account = Account.CreateAgent(TenantId, "hash", Now);

        account.TenantId.ShouldBe(TenantId);
        account.Role.ShouldBe(AccountRole.Agent);
        account.Email.ShouldBe($"agent+{TenantId}@ai-appointment.internal");
        account.Status.ShouldBe(EntityStatus.Active);
    }

    [Fact]
    public void UpdateDetails_changes_name()
    {
        var account = Account.CreateOwner(TenantId, "Elvin", "elvin@business.az", "hash", Now);
        var later = Now + Duration.FromMinutes(1);

        account.UpdateDetails("Elvin M.", later);

        account.Name.ShouldBe("Elvin M.");
        account.UpdatedAt.ShouldBe(later);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void UpdateDetails_throws_when_name_is_blank(string name)
    {
        var account = Account.CreateOwner(TenantId, "Elvin", "elvin@business.az", "hash", Now);

        Should.Throw<ArgumentException>(() => account.UpdateDetails(name, Now));
    }

    [Fact]
    public void ResetPassword_replaces_hash()
    {
        var account = Account.CreateOwner(TenantId, "Elvin", "elvin@business.az", "hash", Now);
        var later = Now + Duration.FromMinutes(1);

        account.ResetPassword("new-hash", later);

        account.PasswordHash.ShouldBe("new-hash");
        account.UpdatedAt.ShouldBe(later);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void ResetPassword_throws_when_hash_is_blank(string hash)
    {
        var account = Account.CreateOwner(TenantId, "Elvin", "elvin@business.az", "hash", Now);

        Should.Throw<ArgumentException>(() => account.ResetPassword(hash, Now));
    }

    [Fact]
    public void Deactivate_then_Reactivate_round_trips_status()
    {
        var account = Account.AddStaff(TenantId, "Rasim", "rasim@business.az", "hash", Now);

        account.Deactivate(Now);
        account.Status.ShouldBe(EntityStatus.Inactive);

        account.Reactivate(Now);
        account.Status.ShouldBe(EntityStatus.Active);
    }
}
