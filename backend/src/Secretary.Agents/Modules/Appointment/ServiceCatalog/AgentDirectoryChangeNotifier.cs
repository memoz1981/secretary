using Secretary.Application.Abstractions;

namespace Secretary.Agents.ServiceCatalog;

/// <summary>Registered as a singleton — stateless itself, just invalidates the shared static
/// stores. Called after every Services-page, Providers-page, Products-page, hours or delivery
/// settings change, from whatever request happened to make it (the Owner via the web app),
/// regardless of whether an agent call is in progress for that tenant right now.
///
/// One coarse signal for every module's stores, as the interface says: a stale answer is one
/// given to a caller on the phone, and an unnecessary refresh costs a query.</summary>
internal sealed class AgentDirectoryChangeNotifier : IAgentDirectoryChangeNotifier
{
    public void NotifyChanged(int tenantId)
    {
        ServiceCatalogStore.Invalidate(tenantId);
        ProviderDirectoryStore.Invalidate(tenantId);
        Orders.OrderDirectoryStore.Invalidate(tenantId);
    }
}
