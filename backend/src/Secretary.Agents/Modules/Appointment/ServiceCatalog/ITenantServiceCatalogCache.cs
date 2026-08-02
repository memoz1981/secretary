using Secretary.Application.Dtos;

namespace Secretary.Agents.ServiceCatalog;

/// <summary>Flow G: loaded once per tenant on cold start, refreshed only when that tenant's
/// Services page changes (via IServiceCatalogChangeNotifier) — never queried live per call.
/// Scoped per DI request/connection like any other Application-layer consumer, but backed by
/// a process-wide store so the cache actually persists across calls.</summary>
public interface ITenantServiceCatalogCache
{
    /// <summary>Returns the current tenant's cached catalog, loading it from
    /// ServiceOfferingService on first access for that tenant.</summary>
    Task<IReadOnlyList<ServiceOfferingResponse>> GetCatalogAsync(CancellationToken cancellationToken);
}
