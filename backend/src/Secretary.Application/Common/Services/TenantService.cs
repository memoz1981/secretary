using Secretary.Application.Abstractions;
using Secretary.Application.Abstractions.Persistence;
using Secretary.Application.Dtos;
using Secretary.Domain.Entities;
using Secretary.Domain.Enums;
using Secretary.Domain.Exceptions;
using NodaTime;

namespace Secretary.Application.Services;

/// <summary>Platform-admin tenant management (functionality-spec.md Flow J), plus the
/// tenant's own self-service (Admin page): GetCurrentAsync/UpdateCurrentAsync operate on the
/// caller's own tenant resolved from ICurrentTenantProvider.</summary>
public sealed class TenantService
{
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ICurrentTenantProvider _currentTenant;

    public TenantService(IUnitOfWork uow, IClock clock, IPasswordHasher passwordHasher, ICurrentTenantProvider currentTenant)
    {
        _uow = uow;
        _clock = clock;
        _passwordHasher = passwordHasher;
        _currentTenant = currentTenant;
    }

    public async Task<CreateTenantResult> CreateAsync(CreateTenantRequest request, CancellationToken cancellationToken)
    {
        // TC-TEAM-06's fix applies here too — the owner email goes through the same
        // globally-unique Account.Email constraint.
        if (await _uow.Accounts.GetByEmailAsync(request.OwnerEmail, cancellationToken) is not null)
        {
            throw new EmailAlreadyInUseException(request.OwnerEmail);
        }

        var now = _clock.GetCurrentInstant();
        var tenant = Tenant.Create(request.Name, request.Timezone, request.PhoneLine, now);
        await _uow.Tenants.AddAsync(tenant, cancellationToken);

        // The tenant's identity Id only exists after the first save — the owner/agent
        // accounts below need it as their FK.
        await _uow.SaveChangesAsync(cancellationToken);

        var owner = Account.CreateOwner(
            tenant.Id, request.OwnerName, request.OwnerEmail, _passwordHasher.Hash(request.OwnerPassword), now);
        await _uow.Accounts.AddAsync(owner, cancellationToken);

        var agentApiKey = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        var agent = Account.CreateAgent(tenant.Id, _passwordHasher.Hash(agentApiKey), now);
        await _uow.Accounts.AddAsync(agent, cancellationToken);

        await _uow.SaveChangesAsync(cancellationToken);
        return new CreateTenantResult(ToResponse(tenant), owner.Id, agent.Id, agentApiKey);
    }

    public async Task<TenantResponse> GetAsync(int id, CancellationToken cancellationToken)
    {
        var tenant = await _uow.Tenants.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException(nameof(Tenant), id);
        return ToResponse(tenant);
    }

    public async Task<IReadOnlyList<TenantResponse>> ListAsync(string? searchText, CancellationToken cancellationToken)
    {
        var tenants = await _uow.Tenants.SearchAsync(searchText, cancellationToken);
        return tenants.Select(ToResponse).ToList();
    }

    /// <summary>Backs the platform-admin Tenant Detail page's "Owner accounts" card
    /// (handoff.md's platform-tenant-detail mockup). AccountService.ListAsync can't serve this
    /// — it resolves the tenant from the ambient ICurrentTenantProvider, which a PlatformAdmin
    /// caller never has. Account isn't globally tenant-filtered (see AccountService's own
    /// note), so GetByTenantAsync's explicit tenantId parameter works here unmodified.</summary>
    public async Task<IReadOnlyList<AccountResponse>> GetOwnerAccountsAsync(int id, CancellationToken cancellationToken)
    {
        if (await _uow.Tenants.GetByIdAsync(id, cancellationToken) is null)
        {
            throw new NotFoundException(nameof(Tenant), id);
        }

        var accounts = await _uow.Accounts.GetByTenantAsync(id, cancellationToken);
        return accounts
            .Where(a => a.Role == AccountRole.Owner)
            .Select(a => new AccountResponse(a.Id, a.Name, a.Email, a.Role, a.Status))
            .ToList();
    }

    public async Task<TenantResponse> UpdateAsync(int id, UpdateTenantRequest request, CancellationToken cancellationToken)
    {
        var tenant = await _uow.Tenants.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException(nameof(Tenant), id);

        tenant.UpdateDetails(request.Name, request.Timezone, request.PhoneLine, _clock.GetCurrentInstant());
        await _uow.SaveChangesAsync(cancellationToken);
        return ToResponse(tenant);
    }

    /// <summary>The caller's own tenant — backs the tenant-facing Admin page.</summary>
    public async Task<TenantResponse> GetCurrentAsync(CancellationToken cancellationToken)
    {
        var tenant = await _uow.Tenants.GetByIdAsync(RequireTenant(), cancellationToken)
            ?? throw new NotFoundException(nameof(Tenant), RequireTenant());
        return ToResponse(tenant);
    }

    /// <summary>Tenant self-service: an Owner updates their own business's name/phone/timezone.</summary>
    public async Task<TenantResponse> UpdateCurrentAsync(UpdateTenantRequest request, CancellationToken cancellationToken)
        => await UpdateAsync(RequireTenant(), request, cancellationToken);

    public async Task DeactivateAsync(int id, CancellationToken cancellationToken)
    {
        var tenant = await _uow.Tenants.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException(nameof(Tenant), id);

        tenant.Deactivate(_clock.GetCurrentInstant());
        await _uow.SaveChangesAsync(cancellationToken);
    }

    public async Task ReactivateAsync(int id, CancellationToken cancellationToken)
    {
        var tenant = await _uow.Tenants.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException(nameof(Tenant), id);

        tenant.Reactivate(_clock.GetCurrentInstant());
        await _uow.SaveChangesAsync(cancellationToken);
    }

    private int RequireTenant()
        => _currentTenant.TenantId ?? throw new InvalidOperationException("This operation requires a tenant-scoped caller.");

    private static TenantResponse ToResponse(Tenant tenant)
        => new(tenant.Id, tenant.Name, tenant.Timezone, tenant.PhoneLine, tenant.Status, tenant.CreatedAt);
}
