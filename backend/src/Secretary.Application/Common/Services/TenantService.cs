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

    /// <summary>Open every day to start with, the window the whole system used before hours were
    /// a per-tenant thing. Wrong for most businesses and visibly so, which is the point: a
    /// tenant who has not set their hours sees a week they can obviously correct, rather than a
    /// system that quietly refuses every caller.</summary>
    private static readonly IsoDayOfWeek[] DefaultWeek =
    [
        IsoDayOfWeek.Monday, IsoDayOfWeek.Tuesday, IsoDayOfWeek.Wednesday, IsoDayOfWeek.Thursday,
        IsoDayOfWeek.Friday, IsoDayOfWeek.Saturday, IsoDayOfWeek.Sunday,
    ];

    public async Task<CreateTenantResult> CreateAsync(CreateTenantRequest request, CancellationToken cancellationToken)
    {
        // TC-TEAM-06's fix applies here too — the owner email goes through the same
        // globally-unique Account.Email constraint.
        if (await _uow.Accounts.GetByEmailAsync(request.OwnerEmail, cancellationToken) is not null)
        {
            throw new EmailAlreadyInUseException(request.OwnerEmail);
        }

        var now = _clock.GetCurrentInstant();
        var tenant = Tenant.Create(
            request.Name, request.Timezone, request.PhoneLine, request.ShowCallCosts, now);
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

        // Appointment on by default, because it is the only module that exists and a tenant with
        // none can log in and reach nothing at all. The platform admin adds and removes the rest.
        // Revisit when a second module ships and "which did they buy" becomes a real question.
        await _uow.TenantModules.AddAsync(
            TenantModule.Grant(tenant.Id, Module.Appointment, now), cancellationToken);

        // Seven days of default hours, without which the tenant is closed all week.
        //
        // A day with no row is closed — deliberately, so a tenant who has never set their hours
        // offers nothing rather than everything. The consequence is that a tenant created
        // without rows can neither offer an appointment nor promise a delivery, and says so with
        // no hint as to why. That is not hypothetical: it is what a brand-new tenant did on its
        // first order call, answering CLOSED_THAT_DAY to every day the caller suggested.
        //
        // The Administration page is where these get changed; this is only so the business works
        // on day one.
        foreach (var day in DefaultWeek)
        {
            await _uow.BusinessHours.AddAsync(
                BusinessHours.Open(tenant.Id, day, new LocalTime(9, 0), new LocalTime(21, 0), now),
                cancellationToken);
        }

        await _uow.SaveChangesAsync(cancellationToken);
        return new CreateTenantResult(
            ToResponse(tenant, [Module.Appointment]), owner.Id, agent.Id, agentApiKey);
    }

    public async Task<TenantResponse> GetAsync(int id, CancellationToken cancellationToken)
    {
        var tenant = await _uow.Tenants.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException(nameof(Tenant), id);

        return ToResponse(tenant, await _uow.TenantModules.GetEnabledModulesAsync(id, cancellationToken));
    }

    /// <summary>The tenant list, with what each one can actually use.
    ///
    /// Modules are what the platform admin is really managing, and the list showed everything
    /// except them — you had to open a tenant to find out what they had bought. One query for
    /// the grants rather than one per row: the list is unpaged and a query per tenant would grow
    /// with the customer base.</summary>
    public async Task<IReadOnlyList<TenantResponse>> ListAsync(string? searchText, CancellationToken cancellationToken)
    {
        var tenants = await _uow.Tenants.SearchAsync(searchText, cancellationToken);
        if (tenants.Count == 0)
        {
            return [];
        }

        var grants = await _uow.TenantModules.GetAllAsync(cancellationToken);
        var byTenant = grants
            .Where(g => g.Status == EntityStatus.Active)
            .GroupBy(g => g.TenantId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<Module>)g.Select(x => x.Module).OrderBy(m => m).ToList());

        return tenants
            .Select(t => ToResponse(t, byTenant.GetValueOrDefault(t.Id, [])))
            .ToList();
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

        tenant.UpdateDetails(
            request.Name, request.Timezone, request.PhoneLine, request.ShowCallCosts,
            _clock.GetCurrentInstant());
        await _uow.SaveChangesAsync(cancellationToken);
        return ToResponse(tenant, await _uow.TenantModules.GetEnabledModulesAsync(id, cancellationToken));
    }

    /// <summary>The caller's own tenant — backs the tenant-facing Admin page.</summary>
    public async Task<TenantResponse> GetCurrentAsync(CancellationToken cancellationToken)
    {
        var tenant = await _uow.Tenants.GetByIdAsync(RequireTenant(), cancellationToken)
            ?? throw new NotFoundException(nameof(Tenant), RequireTenant());
        return ToResponse(tenant, await _uow.TenantModules.GetEnabledModulesAsync(tenant.Id, cancellationToken));
    }

    /// <summary>Tenant self-service: an Owner updates their own business's name/phone/timezone.</summary>
    /// <summary>⚠ Keeps ShowCallCosts exactly as it was. A tenant editing their own name and
    /// timezone does not get to decide whether they may see our costs, and the way to guarantee
    /// that is to carry the stored value across rather than to trust a field not to arrive.</summary>
    public async Task<TenantResponse> UpdateCurrentAsync(
        UpdateOwnTenantRequest request, CancellationToken cancellationToken)
    {
        var tenantId = RequireTenant();
        var tenant = await _uow.Tenants.GetByIdAsync(tenantId, cancellationToken)
            ?? throw new NotFoundException(nameof(Tenant), tenantId);

        return await UpdateAsync(
            tenantId,
            new UpdateTenantRequest(request.Name, request.Timezone, request.PhoneLine, tenant.ShowCallCosts),
            cancellationToken);
    }

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

    /// <summary>Modules are passed in rather than loaded here, so a list of tenants costs one
    /// query for all their grants instead of one per row.</summary>
    private static TenantResponse ToResponse(Tenant tenant, IReadOnlyList<Module> enabledModules)
        => new(tenant.Id, tenant.Name, tenant.Timezone, tenant.PhoneLine, tenant.ShowCallCosts,
            tenant.Status, tenant.CreatedAt, enabledModules);
}
