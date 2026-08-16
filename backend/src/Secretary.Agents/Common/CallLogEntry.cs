using Secretary.Application.Pricing;
using Secretary.Domain.Enums;
using NodaTime;

namespace Secretary.Agents;

/// <summary>Everything the orchestrator knows about a call that just ended, regardless of which
/// module served it. What it does not know — which customer, which order, which appointment —
/// the module adds from its own per-call state before writing its own row.</summary>
/// <param name="Classification">Appointment-shaped, and derived by the orchestrator from
/// appointment tool names. The order line ignores it: whether an order came out of a call is a
/// foreign key on ord.Calls, which cannot drift from the truth the way a label can.</param>
/// <param name="Transcript">What the CALLER said, timestamped, one line per turn — not both
/// sides. The providers transcribe what they hear as part of the session; transcribing what they
/// say back is a separate option and a separate bill that nothing has asked for. Null when the
/// module did not ask for caller transcription, which is most of them.</param>
public sealed record CallLogEntry(
    string CallerPhoneNumber,
    CallClassification Classification,
    CallOutcome Outcome,
    int DurationSeconds,
    int TurnCount,
    int CallerTurnCount,
    string RecordingUrl,
    string? Transcript,
    Instant StartedAt,
    CallPipeline Pipeline,
    IReadOnlyList<ModelUsage> ModelUsages);
