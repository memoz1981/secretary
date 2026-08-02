using Secretary.Application.Abstractions;
using Secretary.Application.Abstractions.Persistence;
using Secretary.Application.Dtos;
using Secretary.Domain.Entities;
using Secretary.Domain.Enums;
using Secretary.Domain.Exceptions;
using NodaTime;

namespace Secretary.Application.Services;

/// <summary>Tenant account self-service (Admin page) — Owner only. Accounts exist for audit
/// trail, not per-provider access, so they're deliberately not linked to Providers. Account
/// isn't tenant-filtered automatically at the persistence layer (see ITenantRepository's
/// note — login needs to search across tenants), so this service resolves the current
/// tenant itself via ICurrentTenantProvider rather than relying on a global query filter.</summary>
public sealed class AccountService
{
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ICurrentTenantProvider _currentTenant;

    public AccountService(IUnitOfWork uow, IClock clock, IPasswordHasher passwordHasher, ICurrentTenantProvider currentTenant)
    {
        _uow = uow;
        _clock = clock;
        _passwordHasher = passwordHasher;
        _currentTenant = currentTenant;
    }

    public async Task<IReadOnlyList<AccountResponse>> ListAsync(CancellationToken cancellationToken)
    {
        var tenantId = RequireTenant();
        var accounts = await _uow.Accounts.GetByTenantAsync(tenantId, cancellationToken);
        return accounts
            .Where(a => a.Status == EntityStatus.Active && a.Role != AccountRole.Agent)
            .Select(ToResponse)
            .ToList();
    }

    public async Task<AccountResponse> AddStaffAsync(AddStaffRequest request, CancellationToken cancellationToken)
    {
        var tenantId = RequireTenant();

        // TC-TEAM-06: check proactively so a duplicate email surfaces as a clean validation
        // error, not a raw unique-constraint DB exception.
        if (await _uow.Accounts.GetByEmailAsync(request.Email, cancellationToken) is not null)
        {
            throw new EmailAlreadyInUseException(request.Email);
        }

        var account = Account.AddStaff(
            tenantId, request.Name, request.Email, _passwordHasher.Hash(request.Password), _clock.GetCurrentInstant());

        await _uow.Accounts.AddAsync(account, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);
        return ToResponse(account);
    }

    public async Task<AccountResponse> UpdateAsync(int id, UpdateAccountRequest request, CancellationToken cancellationToken)
    {
        var account = await GetOwnedAsync(id, cancellationToken);
        account.UpdateDetails(request.Name, _clock.GetCurrentInstant());
        await _uow.SaveChangesAsync(cancellationToken);
        return ToResponse(account);
    }

    /// <summary>Owner self-service password reset for any account in their tenant.</summary>
    public async Task ResetPasswordAsync(int id, ResetAccountPasswordRequest request, CancellationToken cancellationToken)
    {
        var account = await GetOwnedAsync(id, cancellationToken);
        account.ResetPassword(_passwordHasher.Hash(request.Password), _clock.GetCurrentInstant());
        await _uow.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Soft-deactivates rather than deletes — accounts are the audit trail, so the
    /// row (and every record referencing it) must survive. Login rejects inactive accounts.</summary>
    public async Task RemoveAsync(int id, CancellationToken cancellationToken)
    {
        var account = await GetOwnedAsync(id, cancellationToken);
        account.Deactivate(_clock.GetCurrentInstant());
        await _uow.SaveChangesAsync(cancellationToken);
    }

    private async Task<Account> GetOwnedAsync(int id, CancellationToken cancellationToken)
    {
        var tenantId = RequireTenant();
        var account = await _uow.Accounts.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException(nameof(Account), id);

        if (account.TenantId != tenantId)
        {
            throw new TenantMismatchException(nameof(Account), id);
        }

        return account;
    }

    private int RequireTenant()
        => _currentTenant.TenantId ?? throw new InvalidOperationException("This operation requires a tenant-scoped caller.");

    private static AccountResponse ToResponse(Account account)
        => new(account.Id, account.Name, account.Email, account.Role, account.Status);
}
