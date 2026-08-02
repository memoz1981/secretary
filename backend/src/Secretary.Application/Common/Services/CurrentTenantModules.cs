using Secretary.Application.Abstractions;
using Secretary.Application.Abstractions.Persistence;
using Secretary.Domain.Enums;

namespace Secretary.Application.Services;

/// <summary>Scoped, so the lookup happens once per request however many times it is asked.</summary>
public sealed class CurrentTenantModules : ICurrentTenantModules
{
    private readonly ICurrentTenantProvider _currentTenant;
    private readonly IUnitOfWork _uow;

    private IReadOnlyList<Module>? _cached;

    public CurrentTenantModules(ICurrentTenantProvider currentTenant, IUnitOfWork uow)
    {
        _currentTenant = currentTenant;
        _uow = uow;
    }

    public async Task<IReadOnlyList<Module>> GetEnabledAsync(CancellationToken cancellationToken)
    {
        if (_cached is not null)
        {
            return _cached;
        }

        // A platform admin belongs to no tenant, so no module is "theirs". They administer the
        // grants rather than using them, and their own screens are not module-scoped.
        _cached = _currentTenant.TenantId is { } tenantId
            ? await _uow.TenantModules.GetEnabledModulesAsync(tenantId, cancellationToken)
            : [];

        return _cached;
    }

    public async Task<bool> HasAsync(Module module, CancellationToken cancellationToken)
        => (await GetEnabledAsync(cancellationToken)).Contains(module);
}
