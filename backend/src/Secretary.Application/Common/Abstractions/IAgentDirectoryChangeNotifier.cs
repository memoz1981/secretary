namespace Secretary.Application.Abstractions;

/// <summary>Flow G: the AI agent holds a tenant's directory — the service list, and which
/// provider performs which service — in memory rather than reading it live on every call,
/// refreshing only when the tenant changes it. ServiceOfferingService and ProviderService call
/// this after every mutation; Agents provides the real cache-invalidating implementation,
/// registered after AddApplicationServices in the composition root so it wins over the no-op
/// default below.
///
/// Deliberately one coarse signal rather than one per cached thing: a stale answer about who
/// cuts hair is a wrong answer given to a caller on the phone, while an unnecessary refresh
/// costs a query measured in milliseconds.</summary>
public interface IAgentDirectoryChangeNotifier
{
    void NotifyChanged(int tenantId);
}

/// <summary>Default when nothing else is wired up (e.g. Application used without Agents) —
/// so the services always have something to call rather than needing a null check.</summary>
public sealed class NullAgentDirectoryChangeNotifier : IAgentDirectoryChangeNotifier
{
    public void NotifyChanged(int tenantId)
    {
    }
}
