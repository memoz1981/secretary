using System.Collections.Concurrent;
using Secretary.Application.Dtos;

namespace Secretary.Agents.ServiceCatalog;

/// <summary>Process-wide backing store shared between TenantServiceCatalogCache (scoped —
/// reads/populates per tenant) and ServiceCatalogChangeNotifier (singleton — invalidates on
/// any Services-page change, from any request). Kept as a plain static class rather than a
/// singleton service so the two very different DI lifetimes above don't have to fight over
/// who owns the dictionary.</summary>
internal static class ServiceCatalogStore
{
    private static readonly ConcurrentDictionary<int, IReadOnlyList<ServiceOfferingResponse>> Cache = new();

    public static bool TryGet(int tenantId, out IReadOnlyList<ServiceOfferingResponse> value)
        => Cache.TryGetValue(tenantId, out value!);

    public static void Set(int tenantId, IReadOnlyList<ServiceOfferingResponse> value) => Cache[tenantId] = value;

    public static void Invalidate(int tenantId) => Cache.TryRemove(tenantId, out _);
}
