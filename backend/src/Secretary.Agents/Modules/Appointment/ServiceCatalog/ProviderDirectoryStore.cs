using System.Collections.Concurrent;
using Secretary.Application.Dtos;

namespace Secretary.Agents.ServiceCatalog;

/// <summary>Process-wide backing store for "who performs service N", shared between
/// TenantProviderDirectory (scoped — reads/populates per tenant) and
/// AgentDirectoryChangeNotifier (singleton — invalidates on any Providers- or Services-page
/// change). Keyed by tenant first so one tenant's edit can drop that tenant's entries without
/// touching anyone else's, and kept as a plain static class for the same reason
/// ServiceCatalogStore is: the two DI lifetimes above shouldn't have to fight over who owns
/// the dictionary.</summary>
internal static class ProviderDirectoryStore
{
    private static readonly ConcurrentDictionary<int, ConcurrentDictionary<int, IReadOnlyList<ProviderResponse>>> Cache = new();

    public static bool TryGet(int tenantId, int serviceOfferingId, out IReadOnlyList<ProviderResponse> value)
    {
        value = [];
        return Cache.TryGetValue(tenantId, out var byService)
            && byService.TryGetValue(serviceOfferingId, out value!);
    }

    public static void Set(int tenantId, int serviceOfferingId, IReadOnlyList<ProviderResponse> value)
        => Cache.GetOrAdd(tenantId, _ => new ConcurrentDictionary<int, IReadOnlyList<ProviderResponse>>())[serviceOfferingId] = value;

    public static void Invalidate(int tenantId) => Cache.TryRemove(tenantId, out _);
}
