namespace Secretary.Voice.Google;

/// <summary>How a Gemini Live call behaves, and how to reach each backend.
///
/// ⚠ The two backends have a section each, and nothing about connecting is shared between them.
/// AI Studio has no project and no region; Vertex has no API key. A flat list let a setting look
/// as if it applied to both — Location read as if it moved AI Studio calls, which it never did —
/// so each section holds only what its own backend reads. Which one a call uses is
/// FeatureFlags.UseVertexAi; everything outside the two sections is how the call behaves, and is
/// the same on either.</summary>
public sealed class GeminiLiveOptions
{
    public const string SectionName = "GeminiLive";

    public AiStudioOptions AiStudio { get; set; } = new();

    public VertexOptions Vertex { get; set; } = new();

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

    /// <summary>Whether to ask Gemini to transcribe the caller.
    ///
    /// Off, and this is an experiment rather than a settled choice. Making start-of-speech eager
    /// took a short answer from 7.3 s to about 4.5 s but did not fix it, and transcription is the
    /// last thing left on the critical path that we put there ourselves: the caller's transcript
    /// and the model's first audio arrive in the same instant, every time, which is consistent
    /// with the reply waiting on the transcription.
    ///
    /// It is not free to turn off. The transcript is what makes a call reviewable afterwards, and
    /// on the OpenAI side pinning the caller's words as Azerbaijani text is what stops the model
    /// drifting into English after a clipped barge-in. If this does not visibly help, turn it
    /// back on — the debuggability is worth more than a guess.</summary>
    public bool TranscribeCaller { get; set; }

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

/// <summary>Google AI Studio: an API key and a model, nothing else.</summary>
public sealed class AiStudioOptions
{
    /// <summary>Secret.</summary>
    public string ApiKey { get; set; } = string.Empty;

    public string Model { get; set; } = "gemini-3.8-live";
}

/// <summary>Vertex AI — the Gemini Enterprise Agent Platform: a service account, a project and a
/// region. Its model names are not AI Studio's, which is why each section names its own.</summary>
public sealed class VertexOptions
{
    // ⚠ The service account as three named secrets, taken from the key file Google hands out:
    //   project_id   → Project
    //   client_email → ClientEmail
    //   private_key  → PrivateKey
    // Nothing else in that file is needed. Named rather than the whole file pasted in as one
    // value, so what each secret holds is visible; and secrets rather than a path to the file,
    // so a release never has to copy a key onto the server by hand. An API key is not an
    // option: Vertex refuses one on a Live connection.

    /// <summary>Secret: the key file's project_id.</summary>
    public string Project { get; set; } = string.Empty;

    /// <summary>Secret: the key file's client_email — which service account this is.</summary>
    public string ClientEmail { get; set; } = string.Empty;

    /// <summary>Secret: the key file's private_key, copied exactly as it appears between the
    /// quotes, "\n" escapes and all. The only part of the file that is truly sensitive.</summary>
    public string PrivateKey { get; set; } = string.Empty;

    /// <summary>us-central1 because it is where the project can reach gemini-3.8-live today:
    /// europe-west4 answered "model not found or no access" for the same project. Europe is
    /// the better region for an Amsterdam server — the server-to-Google leg is on every turn —
    /// and worth trying again once Google opens it up.</summary>
    public string Location { get; set; } = "us-central1";

    public string Model { get; set; } = "gemini-3.8-live";
}
