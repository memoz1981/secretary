using Secretary.Application.Abstractions;
using Secretary.Application.Dtos;
using Secretary.Application.Services;
using Secretary.Application.Tests.TestSupport;
using Secretary.Domain.Entities;
using Secretary.Domain.Exceptions;
using Moq;
using NodaTime;
using Shouldly;
using Xunit;

namespace Secretary.Application.Tests.Services;

public sealed class AuthServiceTests
{
    private const int TenantId = 7;

    private readonly FakeUnitOfWork _uow = new();
    private readonly Mock<IPasswordHasher> _passwordHasher = new();
    private readonly Mock<IJwtTokenGenerator> _tokenGenerator = new();
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        _sut = new AuthService(_uow.Object, _passwordHasher.Object, _tokenGenerator.Object);
    }

    [Fact]
    public async Task LoginAsync_returns_token_when_credentials_are_valid()
    {
        var account = Account.CreateOwner(TenantId, "Elvin", "elvin@business.az", "hashed", Instant.FromUtc(2026, 7, 11, 9, 0));
        _uow.Accounts.Setup(a => a.GetByEmailAsync("elvin@business.az", default)).ReturnsAsync(account);
        _passwordHasher.Setup(p => p.Verify("secret", "hashed")).Returns(true);
        _tokenGenerator.Setup(t => t.GenerateToken(account)).Returns("jwt-token");

        var result = await _sut.LoginAsync(new LoginRequest("elvin@business.az", "secret"), default);

        result.Token.ShouldBe("jwt-token");
        result.AccountId.ShouldBe(account.Id);
        result.TenantId.ShouldBe(account.TenantId);
    }

    [Fact]
    public async Task LoginAsync_throws_when_account_not_found()
    {
        _uow.Accounts.Setup(a => a.GetByEmailAsync(It.IsAny<string>(), default)).ReturnsAsync((Account?)null);

        await Should.ThrowAsync<InvalidCredentialsException>(() => _sut.LoginAsync(new LoginRequest("nobody@business.az", "secret"), default));
    }

    [Fact]
    public async Task LoginAsync_throws_when_password_is_wrong()
    {
        var account = Account.CreateOwner(TenantId, "Elvin", "elvin@business.az", "hashed", Instant.FromUtc(2026, 7, 11, 9, 0));
        _uow.Accounts.Setup(a => a.GetByEmailAsync("elvin@business.az", default)).ReturnsAsync(account);
        _passwordHasher.Setup(p => p.Verify("wrong", "hashed")).Returns(false);

        await Should.ThrowAsync<InvalidCredentialsException>(() => _sut.LoginAsync(new LoginRequest("elvin@business.az", "wrong"), default));
    }

    [Fact]
    public async Task LoginAsync_throws_when_account_is_deactivated()
    {
        // Soft-removed accounts must lose access, not just disappear from the Admin page list.
        var now = Instant.FromUtc(2026, 7, 11, 9, 0);
        var account = Account.AddStaff(TenantId, "Rasim", "rasim@business.az", "hashed", now);
        account.Deactivate(now);

        _uow.Accounts.Setup(a => a.GetByEmailAsync("rasim@business.az", default)).ReturnsAsync(account);
        _passwordHasher.Setup(p => p.Verify("secret", "hashed")).Returns(true);

        await Should.ThrowAsync<InvalidCredentialsException>(() => _sut.LoginAsync(new LoginRequest("rasim@business.az", "secret"), default));
    }

    [Fact]
    public async Task LoginAsync_throws_when_tenant_is_deactivated()
    {
        // TC-TENANT-03: deactivating a tenant must actually block its accounts from logging in.
        var now = Instant.FromUtc(2026, 7, 11, 9, 0);
        var tenant = Tenant.Create("Baku Barbershop", "Asia/Baku", null, showCallCosts: false, now);
        tenant.Deactivate(now);
        var account = Account.CreateOwner(tenant.Id, "Elvin", "elvin@business.az", "hashed", now);

        _uow.Accounts.Setup(a => a.GetByEmailAsync("elvin@business.az", default)).ReturnsAsync(account);
        _uow.Tenants.Setup(t => t.GetByIdAsync(tenant.Id, default)).ReturnsAsync(tenant);
        _passwordHasher.Setup(p => p.Verify("secret", "hashed")).Returns(true);

        await Should.ThrowAsync<TenantInactiveException>(() => _sut.LoginAsync(new LoginRequest("elvin@business.az", "secret"), default));
    }

    [Fact]
    public async Task LoginAsync_succeeds_when_tenant_is_active()
    {
        var now = Instant.FromUtc(2026, 7, 11, 9, 0);
        var tenant = Tenant.Create("Baku Barbershop", "Asia/Baku", null, showCallCosts: false, now);
        var account = Account.CreateOwner(tenant.Id, "Elvin", "elvin@business.az", "hashed", now);

        _uow.Accounts.Setup(a => a.GetByEmailAsync("elvin@business.az", default)).ReturnsAsync(account);
        _uow.Tenants.Setup(t => t.GetByIdAsync(tenant.Id, default)).ReturnsAsync(tenant);
        _passwordHasher.Setup(p => p.Verify("secret", "hashed")).Returns(true);
        _tokenGenerator.Setup(t => t.GenerateToken(account)).Returns("jwt-token");

        var result = await _sut.LoginAsync(new LoginRequest("elvin@business.az", "secret"), default);

        result.Token.ShouldBe("jwt-token");
    }

    [Fact]
    public async Task LoginAsync_succeeds_for_platform_admin_with_no_tenant()
    {
        var now = Instant.FromUtc(2026, 7, 11, 9, 0);
        var account = Account.CreatePlatformAdmin("Product Owner", "owner@platform.com", "hashed", now);

        _uow.Accounts.Setup(a => a.GetByEmailAsync("owner@platform.com", default)).ReturnsAsync(account);
        _passwordHasher.Setup(p => p.Verify("secret", "hashed")).Returns(true);
        _tokenGenerator.Setup(t => t.GenerateToken(account)).Returns("jwt-token");

        var result = await _sut.LoginAsync(new LoginRequest("owner@platform.com", "secret"), default);

        result.Token.ShouldBe("jwt-token");
        _uow.Tenants.Verify(t => t.GetByIdAsync(It.IsAny<int>(), default), Times.Never);
    }

    [Fact]
    public async Task GetMeAsync_returns_name_email_role_and_tenant_name()
    {
        var now = Instant.FromUtc(2026, 7, 11, 9, 0);
        var tenant = Tenant.Create("Baku Barbershop", "Asia/Baku", null, showCallCosts: false, now);
        var account = Account.CreateOwner(tenant.Id, "Elvin Mammadov", "elvin@business.az", "hashed", now);

        _uow.Accounts.Setup(a => a.GetByIdAsync(account.Id, default)).ReturnsAsync(account);
        _uow.Tenants.Setup(t => t.GetByIdAsync(tenant.Id, default)).ReturnsAsync(tenant);

        var result = await _sut.GetMeAsync(account.Id, default);

        result.Name.ShouldBe("Elvin Mammadov");
        result.Email.ShouldBe("elvin@business.az");
        result.TenantId.ShouldBe(tenant.Id);
        result.TenantName.ShouldBe("Baku Barbershop");
    }

    [Fact]
    public async Task GetMeAsync_returns_null_tenant_name_for_platform_admin()
    {
        var now = Instant.FromUtc(2026, 7, 11, 9, 0);
        var account = Account.CreatePlatformAdmin("Product Owner", "owner@platform.com", "hashed", now);
        _uow.Accounts.Setup(a => a.GetByIdAsync(account.Id, default)).ReturnsAsync(account);

        var result = await _sut.GetMeAsync(account.Id, default);

        result.TenantId.ShouldBeNull();
        result.TenantName.ShouldBeNull();
        _uow.Tenants.Verify(t => t.GetByIdAsync(It.IsAny<int>(), default), Times.Never);
    }

    [Fact]
    public async Task GetMeAsync_throws_when_account_not_found()
    {
        _uow.Accounts.Setup(a => a.GetByIdAsync(It.IsAny<int>(), default)).ReturnsAsync((Account?)null);

        await Should.ThrowAsync<NotFoundException>(() => _sut.GetMeAsync(42, default));
    }
}
