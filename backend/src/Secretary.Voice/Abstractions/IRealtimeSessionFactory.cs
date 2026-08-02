using Secretary.Domain.Enums;

namespace Secretary.Voice.Abstractions;

/// <summary>Builds the session for a given pipeline. One implementation per provider, each
/// declaring which pipelines it serves, so the composition root wires them without a switch
/// statement that has to be edited every time a provider is added.</summary>
public interface IRealtimeSessionFactory
{
    /// <summary>True when this factory can serve the pipeline. Asked in registration order.</summary>
    bool CanServe(CallPipeline pipeline);

    IRealtimeSession Create();
}
