namespace Secretary.Agents;

/// <summary>OpenAI only for now — this app doesn't need Claude/Gemini, but the factory shape
/// keeps adding one later a small, additive change rather than a rewrite.</summary>
public enum AgentProvider
{
    OpenAi,
}
