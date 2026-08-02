using Secretary.Application.Abstractions;
using Secretary.Application.Dtos;
using Secretary.Application.Services;

namespace Secretary.Agents.ServiceCatalog;

internal sealed class TenantServiceCatalogCache : ITenantServiceCatalogCache
{
    private readonly ServiceOfferingService _serviceOfferingService;
    private readonly ICurrentTenantProvider _currentTenant;

    public TenantServiceCatalogCache(ServiceOfferingService serviceOfferingService, ICurrentTenantProvider currentTenant)
    {
        _serviceOfferingService = serviceOfferingService;
        _currentTenant = currentTenant;
    }

    public async Task<IReadOnlyList<ServiceOfferingResponse>> GetCatalogAsync(CancellationToken cancellationToken)
    {
        var tenantId = _currentTenant.TenantId
            ?? throw new InvalidOperationException("This operation requires a tenant-scoped caller.");

        if (ServiceCatalogStore.TryGet(tenantId, out var cached))
        {
            return cached;
        }

        var fresh = await _serviceOfferingService.ListAsync(cancellationToken);
        ServiceCatalogStore.Set(tenantId, fresh);
        return fresh;
    }
}
