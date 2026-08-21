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

public sealed class TenantServiceTests
{
    private static readonly Instant Now = Instant.FromUtc(2026, 7, 11, 9, 0);
    private const int CurrentTenantId = 7;

    private readonly FakeUnitOfWork _uow = new();
    private readonly Mock<IPasswordHasher> _passwordHasher = new();
    private readonly TenantService _sut;

    public TenantServiceTests()
    {
        _passwordHasher.Setup(p => p.Hash(It.IsAny<string>())).Returns<string>(s => $"hashed:{s}");
        _sut = new TenantService(
            _uow.Object, new FakeClock(Now), _passwordHasher.Object, new FakeCurrentTenantProvider(CurrentTenantId));
    }

    [Fact]
    public async Task CreateAsync_creates_tenant_owner_and_agent_accounts()
    {
        var request = new CreateTenantRequest(
            "Baku Barbershop", "Asia/Baku", null, ShowCallCosts: false,
            "Elvin", "elvin@business.az", "ownerpw");

        var result = await _sut.CreateAsync(request, default);

        result.Tenant.Name.ShouldBe("Baku Barbershop");
        result.AgentApiKey.ShouldNotBeNullOrWhiteSpace();

        _uow.Tenants.Verify(t => t.AddAsync(It.IsAny<Tenant>(), default), Times.Once);
        _uow.Accounts.Verify(a => a.AddAsync(It.IsAny<Account>(), default), Times.Exactly(2));

        // Saved twice: once so the tenant's identity Id exists for the account FKs,
        // once for the accounts themselves.
        _uow.UnitOfWork.Verify(u => u.SaveChangesAsync(default), Times.Exactly(2));
    }

    [Fact]
    public async Task CreateAsync_throws_when_owner_email_already_in_use()
    {
        var existing = Account.CreateOwner(1, "Someone Else", "elvin@business.az", "hash", Now);
        _uow.Accounts.Setup(a => a.GetByEmailAsync("elvin@business.az", default)).ReturnsAsync(existing);

        var request = new CreateTenantRequest(
            "Baku Barbershop", "Asia/Baku", null, ShowCallCosts: false,
            "Elvin", "elvin@business.az", "ownerpw");

        await Should.ThrowAsync<EmailAlreadyInUseException>(() => _sut.CreateAsync(request, default));
        _uow.Tenants.Verify(t => t.AddAsync(It.IsAny<Tenant>(), default), Times.Never);
    }

    [Fact]
    public async Task GetAsync_throws_when_tenant_not_found()
    {
        _uow.Tenants.Setup(t => t.GetByIdAsync(It.IsAny<int>(), default)).ReturnsAsync((Tenant?)null);

        await Should.ThrowAsync<NotFoundException>(() => _sut.GetAsync(42, default));
    }

    [Fact]
    public async Task UpdateAsync_updates_and_returns_tenant()
    {
        var tenant = Tenant.Create("Old Name", "Asia/Baku", null, showCallCosts: false, Now);
        _uow.Tenants.Setup(t => t.GetByIdAsync(tenant.Id, default)).ReturnsAsync(tenant);

        var result = await _sut.UpdateAsync(tenant.Id, new UpdateTenantRequest("New Name", "Asia/Baku", "+994111", ShowCallCosts: false), default);

        result.Name.ShouldBe("New Name");
        result.PhoneLine.ShouldBe("+994111");
    }

    [Fact]
    public async Task GetCurrentAsync_reads_the_callers_own_tenant()
    {
        var tenant = Tenant.Create("Baku Barbershop", "Asia/Baku", null, showCallCosts: false, Now);
        _uow.Tenants.Setup(t => t.GetByIdAsync(CurrentTenantId, default)).ReturnsAsync(tenant);

        var result = await _sut.GetCurrentAsync(default);

        result.Name.ShouldBe("Baku Barbershop");
    }

    [Fact]
    public async Task UpdateCurrentAsync_updates_the_callers_own_tenant()
    {
        var tenant = Tenant.Create("Old Name", "Asia/Baku", null, showCallCosts: false, Now);
        _uow.Tenants.Setup(t => t.GetByIdAsync(CurrentTenantId, default)).ReturnsAsync(tenant);

        var result = await _sut.UpdateCurrentAsync(
            new UpdateOwnTenantRequest("New Name", "Asia/Baku", null), default);

        result.Name.ShouldBe("New Name");
        _uow.UnitOfWork.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    /// <summary>⚠ A tenant editing their own details does not get to decide whether they may see
    /// what our calls cost.
    ///
    /// Both edit screens shared one request type, so the moment ShowCallCosts joined it an Owner
    /// could have posted it to their own settings endpoint and switched on the figures the
    /// platform had withheld — no UI change needed, just the field being there. The self-update
    /// takes a type without it and carries the stored value across; this pins that the value
    /// survives an update rather than being reset by one.</summary>
    [Fact]
    public async Task UpdateCurrentAsync_cannot_change_whether_the_tenant_sees_call_costs()
    {
        var tenant = Tenant.Create("Old Name", "Asia/Baku", null, showCallCosts: true, Now);
        _uow.Tenants.Setup(t => t.GetByIdAsync(CurrentTenantId, default)).ReturnsAsync(tenant);

        var result = await _sut.UpdateCurrentAsync(
            new UpdateOwnTenantRequest("New Name", "Asia/Baku", null), default);

        result.ShowCallCosts.ShouldBeTrue();
        tenant.ShowCallCosts.ShouldBeTrue();
    }

    [Fact]
    public async Task DeactivateAsync_then_ReactivateAsync_round_trips()
    {
        var tenant = Tenant.Create("Baku Barbershop", "Asia/Baku", null, showCallCosts: false, Now);
        _uow.Tenants.Setup(t => t.GetByIdAsync(tenant.Id, default)).ReturnsAsync(tenant);

        await _sut.DeactivateAsync(tenant.Id, default);
        tenant.Status.ShouldBe(EntityStatus.Inactive);

        await _sut.ReactivateAsync(tenant.Id, default);
        tenant.Status.ShouldBe(EntityStatus.Active);
    }

    [Fact]
    public async Task ListAsync_delegates_to_repository_search()
    {
        var tenant = Tenant.Create("Baku Barbershop", "Asia/Baku", null, showCallCosts: false, Now);
        _uow.Tenants.Setup(t => t.SearchAsync("Baku", default)).ReturnsAsync([tenant]);

        var result = await _sut.ListAsync("Baku", default);

        result.Count.ShouldBe(1);
        result[0].Name.ShouldBe("Baku Barbershop");
    }

    [Fact]
    public async Task GetOwnerAccountsAsync_returns_only_owner_role_accounts_for_the_tenant()
    {
        var tenant = Tenant.Create("Baku Barbershop", "Asia/Baku", null, showCallCosts: false, Now);
        _uow.Tenants.Setup(t => t.GetByIdAsync(tenant.Id, default)).ReturnsAsync(tenant);

        var owner = Account.CreateOwner(tenant.Id, "Elvin Mammadov", "elvin@business.az", "hash", Now);
        var agent = Account.CreateAgent(tenant.Id, "hash", Now);
        _uow.Accounts.Setup(a => a.GetByTenantAsync(tenant.Id, default)).ReturnsAsync([owner, agent]);

        var result = await _sut.GetOwnerAccountsAsync(tenant.Id, default);

        result.Count.ShouldBe(1);
        result[0].Email.ShouldBe("elvin@business.az");
    }

    [Fact]
    public async Task GetOwnerAccountsAsync_throws_when_tenant_not_found()
    {
        _uow.Tenants.Setup(t => t.GetByIdAsync(It.IsAny<int>(), default)).ReturnsAsync((Tenant?)null);

        await Should.ThrowAsync<NotFoundException>(() => _sut.GetOwnerAccountsAsync(42, default));
    }
}
