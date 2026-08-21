using Secretary.Domain.Enums;

namespace Secretary.Application.Dtos;

public sealed record DashboardSummaryResponse(
    int TotalCalls,
    double ResolvedByAgentRate,
    int ResolvedByAgentCount,
    double EscalationRate,
    int EscalationCount,
    double EscalationResolvedByStaffRate,
    double EscalationAbandonedRate,
    double FailedNoAvailabilityRate,
    double FailedAgentLimitationRate,
    double AverageCallDurationSeconds,
    double AverageTurnsToResolution,
    int AppointmentVolume,
    double ReminderNoAnswerRate,
    // ---- Agent spend for the range (see backend/README.md, "What a call costs") ----
    decimal? TotalCostUsd,
    decimal? AverageCostPerCallUsd,
    // Derived from the range totals, not from averaging each call's own rate: a 20-second call
    // and a 6-minute one contribute equally to an average-of-rates, which makes the number swing
    // on call mix rather than on spend.
    decimal? AverageCostPerMinuteUsd,
    decimal? AverageCostPerAnswerUsd,
    long TotalTokens,
    // Per pipeline, because the whole point of running four of them is to compare. A blended
    // average across architectures that differ tenfold in cost describes none of them.
    IReadOnlyList<PipelineSpendResponse> SpendByPipeline);

/// <param name="Models">Which models actually served these calls, for the legend — a pipeline
/// name says the architecture, this says what was billed.</param>
public sealed record PipelineSpendResponse(
    CallPipeline Pipeline,
    int Calls,
    decimal? TotalCostUsd,
    decimal? AverageCostPerCallUsd,
    decimal? AverageCostPerMinuteUsd,
    double AverageCallDurationSeconds,
    string Models);
