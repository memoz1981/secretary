using Secretary.Application.Services;
using Secretary.Application.Tests.TestSupport;
using Secretary.Domain.Entities;
using Secretary.Domain.Enums;
using Secretary.Domain.ValueObjects;
using Moq;
using NodaTime;
using Shouldly;
using Xunit;

namespace Secretary.Application.Tests.Services;

public sealed class DashboardServiceTests
{
    private const int TenantId = 7;
    private static readonly Instant From = Instant.FromUtc(2026, 7, 6, 0, 0);
    private static readonly Instant To = Instant.FromUtc(2026, 7, 13, 0, 0);

    private readonly FakeUnitOfWork _uow = new();
    private readonly DashboardService _sut;

    public DashboardServiceTests()
    {
        _sut = new DashboardService(_uow.Object);
    }

    private static Call MakeCall(
        CallClassification classification, CallOutcome outcome, int duration = 60, int turns = 3,
        decimal costUsd = 0m, TokenUsage? usage = null)
        => Call.Log(
            TenantId, null, "+994000000", null, classification, outcome, duration, turns, turns, null, "url", null,
            From, "gpt-realtime-2.1", CallPipeline.OpenAiRealtime_2_1, usage ?? TokenUsage.Zero, costUsd, From);

    [Fact]
    public async Task GetSummaryAsync_computes_rates_across_all_calls()
    {
        var calls = new List<Call>
        {
            MakeCall(CallClassification.NewAppointment, CallOutcome.ResolvedByAgent, 100, 6),
            MakeCall(CallClassification.NewAppointment, CallOutcome.ResolvedByAgent, 140, 4),
            MakeCall(CallClassification.UpdateReschedule, CallOutcome.EscalatedResolvedByStaff, 200, 10),
            MakeCall(CallClassification.InquiryOther, CallOutcome.EscalatedAbandoned, 90, 8),
            MakeCall(CallClassification.NewAppointment, CallOutcome.FailedNoAvailability, 60, 5),
            MakeCall(CallClassification.NewAppointment, CallOutcome.FailedAgentLimitation, 60, 5),
        };

        _uow.Calls.Setup(c => c.SearchAsync(From, To, null, null, null, null, default)).ReturnsAsync(calls);
        _uow.Appointments.Setup(a => a.GetForDateRangeAsync(From, To, null, default)).ReturnsAsync([]);

        var result = await _sut.GetSummaryAsync(From, To, default);

        result.TotalCalls.ShouldBe(6);
        result.ResolvedByAgentCount.ShouldBe(2);
        result.ResolvedByAgentRate.ShouldBe(2.0 / 6);
        result.EscalationCount.ShouldBe(2);
        result.EscalationResolvedByStaffRate.ShouldBe(0.5);
        result.EscalationAbandonedRate.ShouldBe(0.5);
        result.FailedNoAvailabilityRate.ShouldBe(0.5);
        result.FailedAgentLimitationRate.ShouldBe(0.5);
        result.AverageCallDurationSeconds.ShouldBe(calls.Average(c => c.DurationSeconds));
        result.AverageTurnsToResolution.ShouldBe(calls.Average(c => c.TurnCount));
    }

    [Fact]
    public async Task GetSummaryAsync_computes_reminder_no_answer_rate_only_over_reminder_calls()
    {
        var calls = new List<Call>
        {
            MakeCall(CallClassification.ReminderConfirmation, CallOutcome.ResolvedByAgent),
            MakeCall(CallClassification.ReminderConfirmation, CallOutcome.NoAnswer),
            MakeCall(CallClassification.NewAppointment, CallOutcome.ResolvedByAgent),
        };

        _uow.Calls.Setup(c => c.SearchAsync(From, To, null, null, null, null, default)).ReturnsAsync(calls);
        _uow.Appointments.Setup(a => a.GetForDateRangeAsync(From, To, null, default)).ReturnsAsync([]);

        var result = await _sut.GetSummaryAsync(From, To, default);

        result.ReminderNoAnswerRate.ShouldBe(0.5);
    }

    [Fact]
    public async Task GetSummaryAsync_returns_zeroes_when_no_calls_at_all()
    {
        _uow.Calls.Setup(c => c.SearchAsync(From, To, null, null, null, null, default)).ReturnsAsync([]);
        _uow.Appointments.Setup(a => a.GetForDateRangeAsync(From, To, null, default)).ReturnsAsync([]);

        var result = await _sut.GetSummaryAsync(From, To, default);

        result.TotalCalls.ShouldBe(0);
        result.ResolvedByAgentRate.ShouldBe(0);
        result.EscalationResolvedByStaffRate.ShouldBe(0);
        result.AverageCallDurationSeconds.ShouldBe(0);
        result.ReminderNoAnswerRate.ShouldBe(0);
        result.TotalCostUsd.ShouldBe(0m);
        result.AverageCostPerMinuteUsd.ShouldBe(0m);
        result.AverageCostPerAnswerUsd.ShouldBe(0m);
        result.TotalTokens.ShouldBe(0);
    }

    [Fact]
    public async Task GetSummaryAsync_totals_what_the_range_cost_to_run()
    {
        var calls = new List<Call>
        {
            MakeCall(CallClassification.NewAppointment, CallOutcome.ResolvedByAgent, 120, 4, 0.40m, new TokenUsage(1_000, 0, 0, 0, 0, 0)),
            MakeCall(CallClassification.Cancellation, CallOutcome.ResolvedByAgent, 60, 2, 0.20m, new TokenUsage(0, 0, 500, 0, 0, 0)),
        };

        _uow.Calls.Setup(c => c.SearchAsync(From, To, null, null, null, null, default)).ReturnsAsync(calls);
        _uow.Appointments.Setup(a => a.GetForDateRangeAsync(From, To, null, default)).ReturnsAsync([]);

        var result = await _sut.GetSummaryAsync(From, To, default);

        result.TotalCostUsd.ShouldBe(0.60m);
        result.AverageCostPerCallUsd.ShouldBe(0.30m);
        result.TotalTokens.ShouldBe(1_500);

        // $0.60 over 180 seconds of talking and 6 answers — taken from the range totals, not by
        // averaging each call's own rate, which would have given $0.20/min here instead.
        result.AverageCostPerMinuteUsd.ShouldBe(0.20m);
        result.AverageCostPerAnswerUsd.ShouldBe(0.10m);
    }

    [Fact]
    public async Task Spend_rates_are_weighted_by_length_not_averaged_per_call()
    {
        // A 30-second call at $0.60/min and a 9½-minute one at $0.284/min. Averaging the two
        // rates would let the trivial call pull as hard as the long one and report $0.44/min;
        // what the range actually cost per minute of talking is $0.30.
        var calls = new List<Call>
        {
            MakeCall(CallClassification.InquiryOther, CallOutcome.ResolvedByAgent, 30, 1, 0.30m),
            MakeCall(CallClassification.NewAppointment, CallOutcome.ResolvedByAgent, 570, 19, 2.70m),
        };

        _uow.Calls.Setup(c => c.SearchAsync(From, To, null, null, null, null, default)).ReturnsAsync(calls);
        _uow.Appointments.Setup(a => a.GetForDateRangeAsync(From, To, null, default)).ReturnsAsync([]);

        var result = await _sut.GetSummaryAsync(From, To, default);

        result.TotalCostUsd.ShouldBe(3.00m);
        result.AverageCostPerMinuteUsd.ShouldBe(0.30m);
        result.AverageCostPerAnswerUsd.ShouldBe(0.15m);
    }

    [Fact]
    public async Task GetSummaryAsync_appointment_volume_excludes_cancelled()
    {
        var confirmed = Appointment.Create(
            TenantId, 10, 20, 30, From, From + Duration.FromMinutes(30), null, AppointmentCreatedBy.Staff, From);
        var cancelled = Appointment.Create(
            TenantId, 11, 21, 31, From, From + Duration.FromMinutes(30), null, AppointmentCreatedBy.Staff, From);
        cancelled.Cancel(From);

        _uow.Calls.Setup(c => c.SearchAsync(From, To, null, null, null, null, default)).ReturnsAsync([]);
        _uow.Appointments.Setup(a => a.GetForDateRangeAsync(From, To, null, default)).ReturnsAsync([confirmed, cancelled]);

        var result = await _sut.GetSummaryAsync(From, To, default);

        result.AppointmentVolume.ShouldBe(1);
    }
}
