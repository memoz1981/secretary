using Secretary.Application.Abstractions;
using Secretary.Application.Abstractions.Persistence;
using Secretary.Application.Dtos;
using Secretary.Domain.Entities;
using Secretary.Domain.Enums;
using Secretary.Domain.Exceptions;

namespace Secretary.Application.Services;

public sealed class AuthService
{
    private readonly IUnitOfWork _uow;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _tokenGenerator;

    public AuthService(IUnitOfWork uow, IPasswordHasher passwordHasher, IJwtTokenGenerator tokenGenerator)
    {
        _uow = uow;
        _passwordHasher = passwordHasher;
        _tokenGenerator = tokenGenerator;
    }

    /// <summary>Routes post-login based on role — the caller (Api layer) decides which
    /// screen to land on from the returned Role/TenantId, per page-inventory.md's Login
    /// notes (platform admin → Tenant List; Owner/Staff → Calendar).</summary>
    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var account = await _uow.Accounts.GetByEmailAsync(request.Email, cancellationToken)
            ?? throw new InvalidCredentialsException();

        if (!_passwordHasher.Verify(request.Password, account.PasswordHash))
        {
            throw new InvalidCredentialsException();
        }

        // Deactivated (soft-removed) accounts must not be able to log in — removal on the
        // Admin page has to actually revoke access, not just hide the row from the list.
        if (account.Status == EntityStatus.Inactive)
        {
            throw new InvalidCredentialsException();
        }

        // TC-TENANT-03: a deactivated tenant's Owner/Staff/Agent accounts must not be able
        // to log in — deactivation has to actually take effect, not just hide the tenant
        // from the platform-admin list while its accounts keep working.
        if (account.TenantId is { } tenantId)
        {
            var tenant = await _uow.Tenants.GetByIdAsync(tenantId, cancellationToken);
            if (tenant is not null && tenant.Status == EntityStatus.Inactive)
            {
                throw new TenantInactiveException();
            }
        }

        var token = _tokenGenerator.GenerateToken(account);
        return new LoginResponse(token, account.Id, account.Role, account.TenantId);
    }

    /// <summary>Backs the frontend's topbar/sidebar (previously showing just the role, since
    /// the JWT deliberately carries no name/business-name — see JwtTokenGenerator). Looked up
    /// fresh per request rather than embedded in the token, so a name or tenant-name edit is
    /// reflected immediately rather than only after the next login.</summary>
    public async Task<MeResponse> GetMeAsync(int accountId, CancellationToken cancellationToken)
    {
        var account = await _uow.Accounts.GetByIdAsync(accountId, cancellationToken)
            ?? throw new NotFoundException(nameof(Account), accountId);

        string? tenantName = null;
        IReadOnlyList<Module> enabledModules = [];

        // Read from the account's own tenant rather than the ambient one: /me is answered for
        // whoever is asking, and a platform admin has no tenant to read modules for.
        if (account.TenantId is { } tenantId)
        {
            var tenant = await _uow.Tenants.GetByIdAsync(tenantId, cancellationToken);
            tenantName = tenant?.Name;
            enabledModules = await _uow.TenantModules.GetEnabledModulesAsync(tenantId, cancellationToken);
        }

        return new MeResponse(
            account.Id, account.Name, account.Email, account.Role, account.TenantId, tenantName, enabledModules);
    }
}
