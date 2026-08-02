using Secretary.Application.Abstractions;
using Secretary.Application.Dtos;
using Secretary.Application.Services;
using Secretary.Application.Tests.TestSupport;
using Secretary.Domain.Entities;
using Secretary.Domain.Enums;
using Secretary.Domain.Exceptions;
using Moq;
using NodaTime;
using NodaTime.Testing;
using Shouldly;
using Xunit;

namespace Secretary.Application.Tests.Services;

public sealed class AccountServiceTests
{
    private static readonly Instant Now = Instant.FromUtc(2026, 7, 11, 9, 0);
    private const int TenantId = 7;
    private const int OtherTenantId = 8;

    private readonly FakeUnitOfWork _uow = new();
    private readonly Mock<IPasswordHasher> _passwordHasher = new();
    private readonly AccountService _sut;

    public AccountServiceTests()
    {
        _passwordHasher.Setup(p => p.Hash(It.IsAny<string>())).Returns("hashed");
        _sut = new AccountService(_uow.Object, new FakeClock(Now), _passwordHasher.Object, new FakeCurrentTenantProvider(TenantId));
    }

    [Fact]
    public async Task AddStaffAsync_creates_an_active_staff_account()
    {
        var request = new AddStaffRequest("Rasim", "rasim@business.az", "pw");

        var result = await _sut.AddStaffAsync(request, default);

        result.Name.ShouldBe("Rasim");
        result.Status.ShouldBe(EntityStatus.Active);
        _uow.Accounts.Verify(a => a.AddAsync(It.IsAny<Account>(), default), Times.Once);
    }

    [Fact]
    public async Task AddStaffAsync_throws_when_email_already_in_use()
    {
        // TC-TEAM-06: a clean validation error, not a raw unique-constraint DB exception.
        var existing = Account.CreateOwner(OtherTenantId, "Someone Else", "rasim@business.az", "hash", Now);
        _uow.Accounts.Setup(a => a.GetByEmailAsync("rasim@business.az", default)).ReturnsAsync(existing);

        var request = new AddStaffRequest("Rasim", "rasim@business.az", "pw");

        await Should.ThrowAsync<EmailAlreadyInUseException>(() => _sut.AddStaffAsync(request, default));
        _uow.Accounts.Verify(a => a.AddAsync(It.IsAny<Account>(), default), Times.Never);
    }

    [Fact]
    public async Task ListAsync_requires_a_tenant_scoped_caller()
    {
        var sut = new AccountService(_uow.Object, new FakeClock(Now), _passwordHasher.Object, new FakeCurrentTenantProvider(null));

        await Should.ThrowAsync<InvalidOperationException>(() => sut.ListAsync(default));
    }

    [Fact]
    public async Task ListAsync_excludes_inactive_accounts_and_the_agent_account()
    {
        var owner = Account.CreateOwner(TenantId, "Elvin", "elvin@business.az", "hash", Now);
        var agent = Account.CreateAgent(TenantId, "hash", Now);
        var removed = Account.AddStaff(TenantId, "Gone", "gone@business.az", "hash", Now);
        removed.Deactivate(Now);
        _uow.Accounts.Setup(a => a.GetByTenantAsync(TenantId, default)).ReturnsAsync([owner, agent, removed]);

        var result = await _sut.ListAsync(default);

        result.Count.ShouldBe(1);
        result[0].Email.ShouldBe("elvin@business.az");
    }

    [Fact]
    public async Task UpdateAsync_throws_tenant_mismatch_for_a_foreign_account()
    {
        var foreignAccount = Account.CreateOwner(OtherTenantId, "Other", "other@business.az", "hash", Now);
        _uow.Accounts.Setup(a => a.GetByIdAsync(foreignAccount.Id, default)).ReturnsAsync(foreignAccount);

        await Should.ThrowAsync<TenantMismatchException>(
            () => _sut.UpdateAsync(foreignAccount.Id, new UpdateAccountRequest("New Name"), default));
    }

    [Fact]
    public async Task UpdateAsync_throws_not_found_when_account_missing()
    {
        _uow.Accounts.Setup(a => a.GetByIdAsync(It.IsAny<int>(), default)).ReturnsAsync((Account?)null);

        await Should.ThrowAsync<NotFoundException>(() => _sut.UpdateAsync(42, new UpdateAccountRequest("Name"), default));
    }

    [Fact]
    public async Task ResetPasswordAsync_rehashes_and_saves()
    {
        var account = Account.AddStaff(TenantId, "Rasim", "rasim@business.az", "old-hash", Now);
        _uow.Accounts.Setup(a => a.GetByIdAsync(account.Id, default)).ReturnsAsync(account);

        await _sut.ResetPasswordAsync(account.Id, new ResetAccountPasswordRequest("new-pw"), default);

        account.PasswordHash.ShouldBe("hashed");
        _uow.UnitOfWork.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task RemoveAsync_soft_deactivates_account_owned_by_current_tenant()
    {
        var account = Account.AddStaff(TenantId, "Rasim", "rasim@business.az", "hash", Now);
        _uow.Accounts.Setup(a => a.GetByIdAsync(account.Id, default)).ReturnsAsync(account);

        await _sut.RemoveAsync(account.Id, default);

        account.Status.ShouldBe(EntityStatus.Inactive);
        _uow.Accounts.Verify(a => a.Remove(It.IsAny<Account>()), Times.Never);
        _uow.UnitOfWork.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }
}
