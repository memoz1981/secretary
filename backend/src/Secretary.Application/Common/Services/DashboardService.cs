using Secretary.Application.Abstractions.Persistence;
using Secretary.Application.Dtos;
using Secretary.Domain.Enums;
using NodaTime;

namespace Secretary.Application.Services;

/// <summary>KPI Dashboard (page-inventory.md). Pulls the whole call/appointment set for the
/// range into memory and aggregates in LINQ — fine at this scale; if call volume grows large
/// enough for this to matter, this is the seam to replace with a database-side aggregate
/// query without touching anything above it.</summary>
public sealed class DashboardService
{
    private readonly IUnitOfWork _uow;

    public DashboardService(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<DashboardSummaryResponse> GetSummaryAsync(Instant from, Instant to, CancellationToken cancellationToken)
    {
        var calls = await _uow.Calls.SearchAsync(from, to, null, null, null, null, cancellationToken);
        var appointments = await _uow.Appointments.GetForDateRangeAsync(from, to, null, cancellationToken);

        var totalCalls = calls.Count;
        var resolvedByAgent = calls.Count(c => c.Outcome == CallOutcome.ResolvedByAgent);
        var escalatedResolved = calls.Count(c => c.Outcome == CallOutcome.EscalatedResolvedByStaff);
        var escalatedAbandoned = calls.Count(c => c.Outcome == CallOutcome.EscalatedAbandoned);
        var escalatedTotal = escalatedResolved + escalatedAbandoned;
        var failedNoAvailability = calls.Count(c => c.Outcome == CallOutcome.FailedNoAvailability);
        var failedAgentLimitation = calls.Count(c => c.Outcome == CallOutcome.FailedAgentLimitation);
        var failedTotal = failedNoAvailability + failedAgentLimitation;

        var reminderCalls = calls.Where(c => c.Classification == CallClassification.ReminderConfirmation).ToList();
        var reminderNoAnswer = reminderCalls.Count(c => c.Outcome == CallOutcome.NoAnswer);

        var totalCostUsd = calls.Sum(c => c.CostUsd);
        var totalSeconds = calls.Sum(c => (long)c.DurationSeconds);
        var totalAnswers = calls.Sum(c => (long)c.TurnCount);
        var totalTokens = calls.Sum(c => (long)c.TokenUsage.TotalTokens);

        return new DashboardSummaryResponse(
            TotalCalls: totalCalls,
            ResolvedByAgentRate: Rate(resolvedByAgent, totalCalls),
            ResolvedByAgentCount: resolvedByAgent,
            EscalationRate: Rate(escalatedTotal, totalCalls),
            EscalationCount: escalatedTotal,
            EscalationResolvedByStaffRate: Rate(escalatedResolved, escalatedTotal),
            EscalationAbandonedRate: Rate(escalatedAbandoned, escalatedTotal),
            FailedNoAvailabilityRate: Rate(failedNoAvailability, failedTotal),
            FailedAgentLimitationRate: Rate(failedAgentLimitation, failedTotal),
            AverageCallDurationSeconds: totalCalls == 0 ? 0 : calls.Average(c => c.DurationSeconds),
            AverageTurnsToResolution: totalCalls == 0 ? 0 : calls.Average(c => c.TurnCount),
            AppointmentVolume: appointments.Count(a => a.AppointmentStatus != Domain.Enums.AppointmentStatus.Cancelled),
            ReminderNoAnswerRate: Rate(reminderNoAnswer, reminderCalls.Count),
            TotalCostUsd: totalCostUsd,
            AverageCostPerCallUsd: Per(totalCostUsd, totalCalls),
            AverageCostPerMinuteUsd: Per(totalCostUsd * 60m, totalSeconds),
            AverageCostPerAnswerUsd: Per(totalCostUsd, totalAnswers),
            TotalTokens: totalTokens,
            SpendByPipeline: SpendByPipeline(calls));
    }

    /// <summary>One row per pipeline that actually served a call in the range.
    ///
    /// Empty pipelines are omitted rather than shown as zeroes: a row reading "$0.00 over 0
    /// calls" looks like a pipeline that costs nothing rather than one nobody dialled.</summary>
    private static IReadOnlyList<PipelineSpendResponse> SpendByPipeline(IReadOnlyList<Domain.Entities.Call> calls)
        => calls
            .GroupBy(call => call.Pipeline)
            .OrderBy(group => group.Key)
            .Select(group =>
            {
                var cost = group.Sum(call => call.CostUsd);
                var seconds = group.Sum(call => (long)call.DurationSeconds);

                return new PipelineSpendResponse(
                    Pipeline: group.Key,
                    Calls: group.Count(),
                    TotalCostUsd: cost,
                    AverageCostPerCallUsd: Per(cost, group.Count()),

                    // Weighted by length, like the range totals — averaging each call's own rate
                    // lets a five-second call count as much as a five-minute one.
                    AverageCostPerMinuteUsd: Per(cost * 60m, seconds),
                    AverageCallDurationSeconds: group.Average(call => call.DurationSeconds),

                    // Distinct because every call in a pipeline names the same models; joining
                    // them per call would repeat the same string once per row.
                    Models: string.Join(
                        ", ",
                        group.Select(call => call.AgentModel)
                            .Where(model => !string.IsNullOrWhiteSpace(model))
                            .Distinct(StringComparer.OrdinalIgnoreCase)));
            })
            .ToList();

    private static double Rate(int count, int total) => total == 0 ? 0 : (double)count / total;

    private static decimal Per(decimal total, long divisor) => divisor == 0 ? 0m : total / divisor;
}
