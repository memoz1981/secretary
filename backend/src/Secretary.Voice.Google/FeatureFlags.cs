namespace Secretary.Voice.Google;

/// <summary>Switches that change which service a call goes to, flipped in configuration.
///
/// Read through IOptionsMonitor by a session that is created once per call, so changing one in
/// appsettings.json takes effect on the next call — no restart, no redeploy.</summary>
public sealed class FeatureFlags
{
    public const string SectionName = "FeatureFlags";

    /// <summary>Gemini Live through Vertex AI (the Gemini Enterprise Agent Platform) rather than
    /// AI Studio.
    ///
    /// Off falls back to AI Studio, which needs nothing but an API key. Each backend keeps its own
    /// key and model in the GeminiLive section, so flipping this is the only edit a switch needs.</summary>
    public bool UseVertexAi { get; set; }
}
