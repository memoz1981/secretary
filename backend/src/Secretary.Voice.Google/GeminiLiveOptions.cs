namespace Secretary.Voice.Google;

/// <summary>Which Gemini backend to talk to, and as whom.
///
/// AI Studio for the demo (an API key, nothing else to set up), the Enterprise Agent Platform —
/// what used to be called Vertex AI — for production, where quotas and SLA are real. The SDK
/// takes both from the same Client constructor, so everything downstream is identical.</summary>
public sealed class GeminiLiveOptions
{
    public const string SectionName = "GeminiLive";

    public GeminiBackend Backend { get; set; } = GeminiBackend.AiStudio;

    /// <summary>AI Studio only.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Enterprise only.</summary>
    public string Project { get; set; } = string.Empty;

    /// <summary>Enterprise only. Frankfurt keeps the model near where the app is hosted.</summary>
    public string Location { get; set; } = "europe-west3";

    /// <summary>Model ids can differ between the two backends, so it lives here rather than
    /// being derived from the backend.</summary>
    public string Model { get; set; } = "gemini-3.1-flash-live-preview";

    /// <summary>One of Gemini's prebuilt voices. Kept configurable because which one carries
    /// Azerbaijani acceptably is exactly the sort of thing only a real call settles.</summary>
    public string Voice { get; set; } = "Aoede";

    public string LanguageCode { get; set; } = "az-AZ";

    /// <summary>Lamiya's turns are a sentence or two. A ceiling costs nothing real and keeps
    /// each request small, the same reasoning as the OpenAI session's.</summary>
    public int MaxOutputTokens { get; set; } = 1200;
}

public enum GeminiBackend
{
    AiStudio,
    Enterprise,
}
