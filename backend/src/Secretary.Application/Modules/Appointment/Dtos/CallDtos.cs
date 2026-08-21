using Secretary.Application.Pricing;
using Secretary.Domain.Enums;
using Secretary.Domain.ValueObjects;
using NodaTime;

namespace Secretary.Application.Dtos;

public sealed record CallResponse(
    int Id,
    int? ClientId,
    string CallerPhoneNumber,
    string? ClientName,
    int? RelatedAppointmentId,
    CallClassification Classification,
    CallOutcome Outcome,
    int DurationSeconds,
    int TurnCount,
    int CallerTurnCount,
    int? WaitTimeSeconds,
    string RecordingUrl,
    Instant StartedAt,
    /// <summary>Null when this caller may not be shown call costs. Which model answered is
    /// the same commercial fact as what it cost — the rates are published — so the two are
    /// withheld together. See CallCostVisibility.</summary>
    string? AgentModel,
    CallPipeline Pipeline,
    TokenUsage TokenUsage,
    decimal? CostUsd)
{
    /// <summary>Null rather than zero for a call too short to divide by — a call that lasted no
    /// measurable time has no meaningful rate, and showing "$0.00/min" for it would drag the
    /// eye to a number that means nothing.</summary>
    public decimal? CostPerMinuteUsd =>
        CostUsd is not { } cost || DurationSeconds <= 0 ? null : cost * 60m / DurationSeconds;

    /// <summary>Cost per answer the agent gave. The more useful of the two rates in practice:
    /// minutes vary with how long the caller thinks, whereas every answer is a model round-trip
    /// billed the whole conversation so far, which is what actually drives the bill.</summary>
    public decimal? CostPerAnswerUsd =>
        CostUsd is not { } perAnswer || TurnCount <= 0 ? null : perAnswer / TurnCount;
}

public sealed record CallDetailResponse(CallResponse Call, string? Transcript);

/// <summary>Logged once, at the end of a call, by the Agent-role caller (or derived
/// server-side for calls staff handle manually — see CallService).</summary>
public sealed record LogCallRequest(
    string CallerPhoneNumber,
    int? RelatedAppointmentId,
    CallClassification Classification,
    CallOutcome Outcome,
    int DurationSeconds,
    int TurnCount,
    int CallerTurnCount,
    int? WaitTimeSeconds,
    string RecordingUrl,
    string? Transcript,
    Instant StartedAt,
    // Which architecture served the call. Separate from the model names because the comparison
    // being run is between pipelines, not between models.
    CallPipeline Pipeline,
    // What each model that served this call consumed. One entry for the realtime path; three for
    // the chained one (recognition, text model, synthesis), because those are billed separately
    // at very different rates. Empty for a call no model served — a staff member handled it —
    // which prices at zero.
    //
    // Each entry's usage must already be split into billable and cached portions: whoever builds
    // this is expected to have used TokenUsage.FromProviderTotals where provider data enters.
    IReadOnlyList<ModelUsage>? ModelUsages);

public sealed record CallSearchRequest(
    Instant? From,
    Instant? To,
    CallClassification? Classification,
    CallOutcome? Outcome,
    int? ProviderId,
    CallPipeline? Pipeline = null);
