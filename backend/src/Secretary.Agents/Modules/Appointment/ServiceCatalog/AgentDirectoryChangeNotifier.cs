using Secretary.Application.Abstractions;

namespace Secretary.Agents.ServiceCatalog;

/// <summary>Registered as a singleton — stateless itself, just invalidates the shared static
/// stores. Called after every Services-page or Providers-page mutation, from whatever request
/// happened to make the change (the Owner via the web app), regardless of whether an agent
/// call is in progress for that tenant right now.</summary>
internal sealed class AgentDirectoryChangeNotifier : IAgentDirectoryChangeNotifier
{
    public void NotifyChanged(int tenantId)
    {
        ServiceCatalogStore.Invalidate(tenantId);
        ProviderDirectoryStore.Invalidate(tenantId);
    }
}
