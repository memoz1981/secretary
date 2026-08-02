using Secretary.Domain.ValueObjects;

namespace Secretary.Voice.Abstractions;

/// <summary>What a realtime provider tells us during a call, in terms the orchestrator can act
/// on without knowing which provider is on the other end.
///
/// The set is deliberately small — it is exactly what the call loop reacts to, derived from the
/// nine OpenAI update types it handled before this abstraction existed. Anything a provider
/// reports that the loop would only log is not worth a member here.
///
/// A closed hierarchy (private constructor, nested cases) so the switch in the orchestrator can
/// be exhaustive: a new provider cannot smuggle in a tenth case the loop silently ignores.</summary>
public abstract record RealtimeEvent
{
    private RealtimeEvent()
    {
    }

    /// <summary>The model began generating a reply. Resets the per-response bookkeeping.</summary>
    public sealed record ResponseStarted : RealtimeEvent;

    /// <summary>What the caller was heard to say. Logged, never acted on — but it is what makes
    /// a recorded call reviewable, and what distinguishes a real turn from the microphone
    /// catching the agent's own voice.</summary>
    public sealed record CallerTranscript(string? Text) : RealtimeEvent;

    /// <summary>Not fatal: the model still has the audio. Worth knowing, because losing the
    /// transcript is what weakens its grip on the language.</summary>
    public sealed record CallerTranscriptFailed(string? Message) : RealtimeEvent;

    /// <summary>PCM16 to relay straight to the caller's speaker.</summary>
    public sealed record AudioOut(byte[] Pcm16) : RealtimeEvent;

    /// <summary>The model wants a tool run. CallId is whatever the provider needs handed back
    /// with the result — an OpenAI call id, a Gemini function-call id — and is opaque here.</summary>
    public sealed record ToolCallRequested(string CallId, string Name, string ArgumentsJson) : RealtimeEvent;

    /// <summary>The reply finished, however it ended. Usage is reported even for cancelled and
    /// failed responses: a reply cut short by barge-in still consumed the whole conversation as
    /// input, and is billed for it.</summary>
    public sealed record ResponseFinished(
        RealtimeResponseOutcome Outcome,
        string? Reason,
        string? FailureMessage,
        TokenUsage? Usage) : RealtimeEvent;

    /// <summary>Server-side voice activity detection says the caller took the turn. Drives
    /// barge-in and the call's question count.</summary>
    public sealed record CallerSpeechStarted : RealtimeEvent;

    public sealed record CallerSpeechStopped : RealtimeEvent;

    /// <summary>A provider error that is not a benign race. Code is provider-specific and used
    /// only for logging and for recognising the races worth ignoring.</summary>
    public sealed record SessionError(string? Code, string? Message) : RealtimeEvent;
}

/// <summary>How a response ended. Only Completed counts as an answer the caller actually
/// heard — a response killed by the rate limiter is not one, and counting it flattered the
/// turns-to-resolution KPI while dividing the call's cost by a number bigger than reality.</summary>
public enum RealtimeResponseOutcome
{
    Completed,
    Cancelled,
    Failed,
    Incomplete,
}
