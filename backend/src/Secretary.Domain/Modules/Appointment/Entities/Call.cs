using Secretary.Domain.Abstractions;
using Secretary.Domain.Enums;
using Secretary.Domain.ValueObjects;
using NodaTime;

namespace Secretary.Domain.Entities;

/// <summary>The permanent record of a single call, written once at the end of the call
/// (Stage 1 — every flow logs a call, success or not). The base Status is always Active:
/// call deletion is not allowed. See Escalation for the transient real-time state of a
/// mid-call transfer that may feed into this record's WaitTimeSeconds.</summary>
public sealed class Call : BaseEntity
{
    public int TenantId { get; private set; }
    public int? ClientId { get; private set; }

    /// <summary>The caller's number as dialed in, independent of whether it resolved to a
    /// Client record — Call Log (Stage 2) shows the raw number whenever a call didn't
    /// resolve to a known client, so this can't be sourced from ClientId alone.</summary>
    public string CallerPhoneNumber { get; private set; }
    public int? RelatedAppointmentId { get; private set; }
    public CallClassification Classification { get; private set; }
    public CallOutcome Outcome { get; private set; }
    public int DurationSeconds { get; private set; }

    /// <summary>Answers: replies the agent actually completed. Together with
    /// <see cref="CallerTurnCount"/> this is the call's question/answer count, and it is the
    /// denominator behind cost-per-answer.</summary>
    public int TurnCount { get; private set; }

    /// <summary>Questions: times the caller took the turn.</summary>
    public int CallerTurnCount { get; private set; }
    public int? WaitTimeSeconds { get; private set; }
    public string RecordingUrl { get; private set; }
    public string? Transcript { get; private set; }
    public Instant StartedAt { get; private set; }

    // ---- What this call cost to run ----
    // See backend/README.md, "What a call costs". Both halves are written once, at the end of
    // the call, and never recalculated: the token counts are what the provider reported, and
    // CostUsd is those counts priced at the rates in force AT THAT MOMENT. Re-deriving the
    // price on read would mean editing a rate in appsettings silently rewrote the cost of
    // every call ever made, so the money figure is a snapshot, not a projection.

    /// <summary>The model that served this call, e.g. "gpt-realtime-2.1" — recorded because
    /// the rate card is per model, and because a model swap is exactly when a jump in the cost
    /// charts needs explaining. A chained call names all three.</summary>
    public string AgentModel { get; private set; }

    /// <summary>Which pipeline served it. The models above say WHAT was billed; this says which
    /// architecture produced the call, which is what the cost and latency comparison is
    /// actually between.</summary>
    public CallPipeline Pipeline { get; private set; }

    public int InputTextTokens { get; private set; }
    public int CachedInputTextTokens { get; private set; }
    public int InputAudioTokens { get; private set; }
    public int CachedInputAudioTokens { get; private set; }
    public int OutputTextTokens { get; private set; }
    public int OutputAudioTokens { get; private set; }

    /// <summary>Priced at logging time and immutable thereafter.</summary>
    public decimal CostUsd { get; private set; }

    public TokenUsage TokenUsage => new(
        InputTextTokens, CachedInputTextTokens, InputAudioTokens,
        CachedInputAudioTokens, OutputTextTokens, OutputAudioTokens);

    private Call(
        int tenantId, int? clientId, string callerPhoneNumber, int? relatedAppointmentId,
        CallClassification classification, CallOutcome outcome, int durationSeconds, int turnCount,
        int callerTurnCount, int? waitTimeSeconds, string recordingUrl, string? transcript,
        Instant startedAt, string agentModel, CallPipeline pipeline, TokenUsage tokenUsage,
        decimal costUsd, Instant now)
    {
        TenantId = tenantId;
        ClientId = clientId;
        CallerPhoneNumber = callerPhoneNumber;
        RelatedAppointmentId = relatedAppointmentId;
        Classification = classification;
        Outcome = outcome;
        DurationSeconds = durationSeconds;
        TurnCount = turnCount;
        CallerTurnCount = callerTurnCount;
        WaitTimeSeconds = waitTimeSeconds;
        RecordingUrl = recordingUrl;
        Transcript = transcript;
        StartedAt = startedAt;
        AgentModel = agentModel;
        Pipeline = pipeline;
        InputTextTokens = tokenUsage.InputTextTokens;
        CachedInputTextTokens = tokenUsage.CachedInputTextTokens;
        InputAudioTokens = tokenUsage.InputAudioTokens;
        CachedInputAudioTokens = tokenUsage.CachedInputAudioTokens;
        OutputTextTokens = tokenUsage.OutputTextTokens;
        OutputAudioTokens = tokenUsage.OutputAudioTokens;
        CostUsd = costUsd;
        InitBase(now);
    }

    private Call()
    {
        RecordingUrl = string.Empty;
        CallerPhoneNumber = string.Empty;
        AgentModel = string.Empty;
    }

    public static Call Log(
        int tenantId, int? clientId, string callerPhoneNumber, int? relatedAppointmentId,
        CallClassification classification, CallOutcome outcome, int durationSeconds, int turnCount,
        int callerTurnCount, int? waitTimeSeconds, string recordingUrl, string? transcript,
        Instant startedAt, string agentModel, CallPipeline pipeline, TokenUsage tokenUsage,
        decimal costUsd, Instant now)
    {
        if (durationSeconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(durationSeconds));
        }

        if (turnCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(turnCount));
        }

        if (callerTurnCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(callerTurnCount));
        }

        if (costUsd < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(costUsd));
        }

        if (string.IsNullOrWhiteSpace(callerPhoneNumber))
        {
            throw new ArgumentException("Caller phone number is required.", nameof(callerPhoneNumber));
        }

        return new Call(
            tenantId, clientId, callerPhoneNumber, relatedAppointmentId, classification, outcome,
            durationSeconds, turnCount, callerTurnCount, waitTimeSeconds, recordingUrl, transcript,
            startedAt, agentModel ?? string.Empty, pipeline, tokenUsage, costUsd, now);
    }
}
