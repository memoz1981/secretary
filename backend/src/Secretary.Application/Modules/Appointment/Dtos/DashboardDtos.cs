using Secretary.Domain.Enums;

namespace Secretary.Application.Dtos;

/// <summary>One outcome, its count, and its share of every call in the range.
///
/// ⚠ The share is computed here rather than on the page. Four tiles each dividing by a total they
/// were handed separately is four chances to divide by a different number, and the first symptom
/// is percentages that do not add up in front of a customer.</summary>
public sealed record CallOutcomeCount(AppointmentCallOutcome Outcome, int Calls, double Share);

public sealed record DashboardSummaryResponse(
    int TotalCalls,

    /// <summary>Every outcome that can occur, whether or not it did. An absent row reads as "no
    /// data"; a zero reads as "this did not happen", and those are different things to know.</summary>
    IReadOnlyList<CallOutcomeCount> ByOutcome,

    // ---- How long the agent spent (see the Call statistics row) ----
    int TotalDurationSeconds,
    double AverageCallDurationSeconds,

    /// <summary>Averaged over resolved calls only. A line whose failures are three seconds long
    /// looks fast in a blended average, which is the opposite of what the number is for: how long
    /// it takes the agent to actually finish something. Null when nothing resolved — an average
    /// over no calls is not zero.</summary>
    double? AverageResolvedCallDurationSeconds,

    /// <summary>Appointments still standing that the agent made in this range. Cancelled ones are
    /// left out: the question is what the line produced, and a booking that was cancelled
    /// produced nothing.</summary>
    int AppointmentsMade,
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
