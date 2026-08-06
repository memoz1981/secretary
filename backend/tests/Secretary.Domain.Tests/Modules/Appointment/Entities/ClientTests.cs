using Secretary.Domain.Entities;
using NodaTime;
using Shouldly;
using Xunit;

namespace Secretary.Domain.Tests.Entities;

public sealed class ClientTests
{
    private const int TenantId = 1;
    private static readonly Instant Now = Instant.FromUtc(2026, 7, 11, 9, 0);

    [Fact]
    public void Create_without_name_leaves_it_null()
    {
        var client = Client.Create(TenantId, "+994000000", Now);

        client.PhoneNumber.ShouldBe("+994000000");
        client.Name.ShouldBeNull();
        client.BlackListed.ShouldBeFalse();
        client.BlackListReason.ShouldBeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Create_throws_when_phone_number_is_blank(string? phoneNumber)
    {
        Should.Throw<ArgumentException>(() => Client.Create(TenantId, phoneNumber!, Now));
    }

    [Fact]
    public void UpdateName_sets_name()
    {
        var client = Client.Create(TenantId, "+994000000", Now);

        client.UpdateName("Tofiq Aliyev", Now);

        client.Name.ShouldBe("Tofiq Aliyev");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void UpdateName_throws_when_blank(string name)
    {
        var client = Client.Create(TenantId, "+994000000", Now);

        Should.Throw<ArgumentException>(() => client.UpdateName(name, Now));
    }

    [Fact]
    public void UpdateDetails_changes_phone_and_name()
    {
        var client = Client.Create(TenantId, "+994000000", Now, "Tofiq");
        var later = Now + Duration.FromMinutes(1);

        client.UpdateDetails("+994111111", "Tofiq Aliyev", later);

        client.PhoneNumber.ShouldBe("+994111111");
        client.Name.ShouldBe("Tofiq Aliyev");
        client.UpdatedAt.ShouldBe(later);
    }

    [Fact]
    public void UpdateDetails_blank_name_becomes_null()
    {
        var client = Client.Create(TenantId, "+994000000", Now, "Tofiq");

        client.UpdateDetails("+994000000", " ", Now);

        client.Name.ShouldBeNull();
    }

    [Fact]
    public void UpdateDetails_throws_when_phone_number_is_blank()
    {
        var client = Client.Create(TenantId, "+994000000", Now);

        Should.Throw<ArgumentException>(() => client.UpdateDetails(" ", null, Now));
    }

    [Fact]
    public void BlackList_sets_flag_and_reason()
    {
        var client = Client.Create(TenantId, "+994000000", Now);

        client.BlackList("repeated no-shows", Now);

        client.BlackListed.ShouldBeTrue();
        client.BlackListReason.ShouldBe("repeated no-shows");
    }

    [Fact]
    public void BlackList_with_blank_reason_stores_null()
    {
        var client = Client.Create(TenantId, "+994000000", Now);

        client.BlackList(" ", Now);

        client.BlackListed.ShouldBeTrue();
        client.BlackListReason.ShouldBeNull();
    }

    [Fact]
    public void UndoBlackList_clears_flag_and_reason()
    {
        var client = Client.Create(TenantId, "+994000000", Now);
        client.BlackList("repeated no-shows", Now);

        client.UndoBlackList(Now);

        client.BlackListed.ShouldBeFalse();
        client.BlackListReason.ShouldBeNull();
    }
}
