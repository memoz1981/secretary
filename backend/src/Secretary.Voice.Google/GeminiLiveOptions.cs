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

    /// <summary>How long a caller has to go quiet before Gemini treats the turn as finished.
    ///
    /// Left on the SDK default this was the whole of the reported "2–5 second delay": tools
    /// answer in under 150 ms and the first audio arrives inside a second, so the wait was
    /// Gemini deciding the caller had stopped — most obvious after a one-word answer like
    /// "xeyr", where there is little speech to be confident about.
    ///
    /// Configurable because it is a genuine trade-off with no right answer from a desk: too
    /// long and the agent feels slow, too short and it talks over someone drawing breath
    /// mid-sentence. The OpenAI session sits at 700 ms, deliberately longer than its own
    /// default, because a false turn-end there kills the in-flight response outright.</summary>
    public int EndOfSpeechSilenceMs { get; set; } = 500;

    /// <summary>Whether Gemini leans towards deciding the caller has started talking.
    ///
    /// High, on measured evidence. Microphone edges logged alongside Gemini's transcripts show
    /// recognition lag scaling inversely with how long the caller spoke: ~1.5 s for a two-second
    /// sentence, but **7.3 s for a 200 ms "xeyr"** — answered 36 ms after the agent stopped. The
    /// caller was never slow; a short burst simply was not being taken for speech.
    ///
    /// This was left on the default for two rounds out of a worry that a keener detector would
    /// mistake the agent's own voice returning through the speakers for the caller. That worry
    /// was reasonable and turned out to cost more than it saved — and the browser applies echo
    /// cancellation on capture. If false barge-ins appear, this is the first thing to put back.</summary>
    public bool EagerStartOfSpeech { get; set; } = true;

    /// <summary>Audio kept from just before speech is detected, so the first syllable is not
    /// clipped off the front of a short answer. Matches the OpenAI session's 300 ms.</summary>
    public int PrefixPaddingMs { get; set; } = 300;

    /// <summary>How many tokens Gemini may spend thinking before it answers. Zero turns it off.
    ///
    /// Off by default, and this is the real cause of the long silences. The timings say so
    /// plainly: a turn needing no decision came back in 1.2 seconds, while "xeyr" to "is there a
    /// master preference" — which forces the model to pick a provider and go check availability —
    /// took 8.3, with the transcript, the turn and the tool call all arriving in the same batch.
    /// Nothing on our side was slow; the model was deliberating before it said anything.
    ///
    /// A receptionist has nothing to deliberate about. The reasoning that matters here is
    /// choosing which tool to call, and the instructions already say when to call each one.
    /// Raise it only if the agent starts making poor choices rather than slow ones.</summary>
    public int ThinkingBudgetTokens { get; set; }
}

public enum GeminiBackend
{
    AiStudio,
    Enterprise,
}
