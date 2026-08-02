using Secretary.Domain.Enums;
using Secretary.Voice.Abstractions;

namespace Secretary.Voice;

/// <summary>Picks the session for a pipeline by asking each registered provider factory in turn.
///
/// A resolver rather than a switch so that adding a provider is a registration in that
/// provider's own DependencyInjection and nothing else — the composition root is the only place
/// that knows the full list, which is what keeps Secretary.Agents free of vendor packages.</summary>
public sealed class RealtimeSessionResolver
{
    private readonly IEnumerable<IRealtimeSessionFactory> _factories;

    public RealtimeSessionResolver(IEnumerable<IRealtimeSessionFactory> factories) => _factories = factories;

    public IRealtimeSession Create(CallPipeline pipeline)
    {
        var factory = _factories.FirstOrDefault(f => f.CanServe(pipeline))
            ?? throw new InvalidOperationException(
                $"No realtime provider is registered for pipeline '{pipeline}'. Register its "
                + "AddXxxRealtime(...) in the composition root, or remove the pipeline from "
                + "VoicePipelineCatalog so it cannot be dialled.");

        return factory.Create();
    }
}
