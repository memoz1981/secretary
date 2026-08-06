namespace Secretary.Domain.Enums;

/// <summary>What the call was about — functionality-spec.md §4.</summary>
public enum CallClassification
{
    NewAppointment,
    UpdateReschedule,
    Cancellation,
    ReminderConfirmation,
    InquiryOther,
}

/// <summary>What happened on the call — functionality-spec.md §4. Deliberately more precise
/// than "success/failure" so the KPI dashboard can break down escalation and failure reasons.
/// NoAnswer is an addition on top of the 5 confirmed there: Flow E's "no answer/voicemail"
/// outbound-reminder case doesn't fit Resolved/Escalated/Failed (nobody answering isn't the
/// agent failing at anything), but Flow E still requires logging that call, and the KPI
/// dashboard's "reminder no-answer rate" needs an outcome to count — flagged as a gap filled
/// in during backend modeling, not a silent reinterpretation of the confirmed 5.</summary>
public enum CallOutcome
{
    ResolvedByAgent,
    EscalatedResolvedByStaff,
    EscalatedAbandoned,
    FailedNoAvailability,
    FailedAgentLimitation,
    NoAnswer,
}

/// <summary>Which voice pipeline served the call.
///
/// The chained architectures — recognition, then a text-only model, then synthesis — were
/// dropped in the rebuild. They were slower, worse at Azerbaijani, and the cost saving did not
/// justify either. Realtime speech-to-speech only; Gemini Live joins here as a second option.
///
/// The numbering stays stable: the value is persisted on every Call row, so a retired member's
/// number must never be reused.</summary>
public enum CallPipeline
{
    /// <summary>Logged before pipelines were recorded, or handled by a human. Not a pipeline.</summary>
    Unknown = 0,

    /// <summary>One OpenAI realtime model handling audio end to end. Fastest and best at
    /// Azerbaijani, because it hears the audio rather than a transcription of it. Also by far
    /// the most expensive, at roughly $0.10 a minute.</summary>
    OpenAiRealtime_2_1 = 1,

    /// <summary>Gemini Live, native audio in and out. Roughly four times cheaper than OpenAI,
    /// and its Azerbaijani quality is entirely unverified — which is why it exists here but is
    /// not dialable yet. Gemini Live speaks a different wire protocol, so enabling it needs a
    /// session implementation of its own, not just an entry in the catalogue.</summary>
    GeminiLive_3_1 = 2,
}
