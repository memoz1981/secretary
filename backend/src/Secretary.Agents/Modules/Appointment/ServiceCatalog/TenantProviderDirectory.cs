using Secretary.Application.Abstractions;
using Secretary.Application.Dtos;
using Secretary.Application.Services;

namespace Secretary.Agents.ServiceCatalog;

internal sealed class TenantProviderDirectory : ITenantProviderDirectory
{
    private readonly ProviderService _providerService;
    private readonly ICurrentTenantProvider _currentTenant;

    public TenantProviderDirectory(ProviderService providerService, ICurrentTenantProvider currentTenant)
    {
        _providerService = providerService;
        _currentTenant = currentTenant;
    }

    public async Task<IReadOnlyList<ProviderResponse>> GetProvidersForServiceAsync(
        int serviceOfferingId, CancellationToken cancellationToken)
    {
        var tenantId = _currentTenant.TenantId
            ?? throw new InvalidOperationException("This operation requires a tenant-scoped caller.");

        if (ProviderDirectoryStore.TryGet(tenantId, serviceOfferingId, out var cached))
        {
            return cached;
        }

        var fresh = await _providerService.ListForOfferingAsync(serviceOfferingId, cancellationToken);
        ProviderDirectoryStore.Set(tenantId, serviceOfferingId, fresh);
        return fresh;
    }
}
