using Secretary.Application.Abstractions.Persistence;
using Secretary.Application.Abstractions;
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
        _sut = new DashboardService(_uow.Object, CostsVisible(_uow.UnitOfWork.Object));
    }

    private static Call MakeCall(
        CallClassification classification, AppointmentCallOutcome outcome, int duration = 60, int turns = 3,
        decimal costUsd = 0m, TokenUsage? usage = null)
        => Call.Log(
            TenantId, null, "+994000000", null, classification, outcome, duration, turns, turns, null, "url", null,
            From, "gpt-realtime-2.1", CallPipeline.OpenAiRealtime_2_1, usage ?? TokenUsage.Zero, costUsd, From);

    /// <summary>⚠ Counts first, shares derived from them — never an average of averages. Four
    /// tiles each dividing by a total handed to them separately is four chances to divide by a
    /// different number, and the first symptom is percentages that do not add to 100 in front of
    /// a customer.</summary>
    [Fact]
    public async Task GetSummaryAsync_counts_every_outcome_and_shares_them_against_the_total()
    {
        var calls = new List<Call>
        {
            MakeCall(CallClassification.NewAppointment, AppointmentCallOutcome.Resolved, 100, 6),
            MakeCall(CallClassification.NewAppointment, AppointmentCallOutcome.Resolved, 140, 4),
            MakeCall(CallClassification.UpdateReschedule, AppointmentCallOutcome.Forwarded, 200, 10),
            MakeCall(CallClassification.InquiryOther, AppointmentCallOutcome.Forwarded, 90, 8),
            MakeCall(CallClassification.NewAppointment, AppointmentCallOutcome.NoAvailability, 60, 5),
            MakeCall(CallClassification.NewAppointment, AppointmentCallOutcome.Unfinished, 60, 5),
        };

        _uow.Calls.Setup(c => c.SearchAsync(From, To, null, null, null, null, default)).ReturnsAsync(calls);
        _uow.Appointments.Setup(a => a.GetForDateRangeAsync(From, To, null, default)).ReturnsAsync([]);

        var result = await _sut.GetSummaryAsync(From, To, default);

        result.TotalCalls.ShouldBe(6);

        int Count(AppointmentCallOutcome outcome) => result.ByOutcome.Single(o => o.Outcome == outcome).Calls;
        double Share(AppointmentCallOutcome outcome) => result.ByOutcome.Single(o => o.Outcome == outcome).Share;

        Count(AppointmentCallOutcome.Resolved).ShouldBe(2);
        Count(AppointmentCallOutcome.Forwarded).ShouldBe(2);
        Count(AppointmentCallOutcome.Unfinished).ShouldBe(1);
        Count(AppointmentCallOutcome.NoAvailability).ShouldBe(1);

        Share(AppointmentCallOutcome.Resolved).ShouldBe(2.0 / 6);
        Share(AppointmentCallOutcome.NoAvailability).ShouldBe(1.0 / 6);

        // The property that makes the percentages worth printing: four outcomes, every call in
        // exactly one of them, and nothing left over.
        result.ByOutcome.Sum(o => o.Calls).ShouldBe(6);
        result.ByOutcome.Sum(o => o.Share).ShouldBe(1.0, 0.000001);

        result.TotalDurationSeconds.ShouldBe(650);
        result.AverageCallDurationSeconds.ShouldBe(calls.Average(c => c.DurationSeconds));
    }

    /// <summary>⚠ Over resolved calls only. A line whose failures last three seconds looks fast
    /// in a blended average, which is the opposite of what this number is for: how long the agent
    /// takes to actually finish something.</summary>
    [Fact]
    public async Task The_resolved_average_ignores_calls_that_did_not_resolve()
    {
        var calls = new List<Call>
        {
            MakeCall(CallClassification.NewAppointment, AppointmentCallOutcome.Resolved, 200),
            MakeCall(CallClassification.NewAppointment, AppointmentCallOutcome.Resolved, 100),
            MakeCall(CallClassification.InquiryOther, AppointmentCallOutcome.Unfinished, 3),
        };

        _uow.Calls.Setup(c => c.SearchAsync(From, To, null, null, null, null, default)).ReturnsAsync(calls);
        _uow.Appointments.Setup(a => a.GetForDateRangeAsync(From, To, null, default)).ReturnsAsync([]);

        var result = await _sut.GetSummaryAsync(From, To, default);

        result.AverageResolvedCallDurationSeconds.ShouldBe(150);
        result.AverageCallDurationSeconds.ShouldBe(101);
    }

    /// <summary>An average over no calls is not zero. Zero would read as a line that resolves
    /// everything instantly.</summary>
    [Fact]
    public async Task The_resolved_average_is_absent_when_nothing_resolved()
    {
        _uow.Calls.Setup(c => c.SearchAsync(From, To, null, null, null, null, default))
            .ReturnsAsync([MakeCall(CallClassification.InquiryOther, AppointmentCallOutcome.Unfinished, 3)]);
        _uow.Appointments.Setup(a => a.GetForDateRangeAsync(From, To, null, default)).ReturnsAsync([]);

        var result = await _sut.GetSummaryAsync(From, To, default);

        result.AverageResolvedCallDurationSeconds.ShouldBeNull();
    }

    [Fact]
    public async Task GetSummaryAsync_returns_zeroes_when_no_calls_at_all()
    {
        _uow.Calls.Setup(c => c.SearchAsync(From, To, null, null, null, null, default)).ReturnsAsync([]);
        _uow.Appointments.Setup(a => a.GetForDateRangeAsync(From, To, null, default)).ReturnsAsync([]);

        var result = await _sut.GetSummaryAsync(From, To, default);

        result.TotalCalls.ShouldBe(0);

        // Every outcome still listed. A tile that vanishes when its count is zero moves the ones
        // beside it, so the page reshuffles between one range and the next.
        result.ByOutcome.Count.ShouldBe(4);
        result.ByOutcome.ShouldAllBe(o => o.Calls == 0 && o.Share == 0);
        result.AverageCallDurationSeconds.ShouldBe(0);
        result.AverageResolvedCallDurationSeconds.ShouldBeNull();
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
            MakeCall(CallClassification.NewAppointment, AppointmentCallOutcome.Resolved, 120, 4, 0.40m, new TokenUsage(1_000, 0, 0, 0, 0, 0)),
            MakeCall(CallClassification.Cancellation, AppointmentCallOutcome.Resolved, 60, 2, 0.20m, new TokenUsage(0, 0, 500, 0, 0, 0)),
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
            MakeCall(CallClassification.InquiryOther, AppointmentCallOutcome.Resolved, 30, 1, 0.30m),
            MakeCall(CallClassification.NewAppointment, AppointmentCallOutcome.Resolved, 570, 19, 2.70m),
        };

        _uow.Calls.Setup(c => c.SearchAsync(From, To, null, null, null, null, default)).ReturnsAsync(calls);
        _uow.Appointments.Setup(a => a.GetForDateRangeAsync(From, To, null, default)).ReturnsAsync([]);

        var result = await _sut.GetSummaryAsync(From, To, default);

        result.TotalCostUsd.ShouldBe(3.00m);
        result.AverageCostPerMinuteUsd.ShouldBe(0.30m);
        result.AverageCostPerAnswerUsd.ShouldBe(0.15m);
    }

    [Fact]
    public async Task GetSummaryAsync_appointments_made_excludes_cancelled()
    {
        var confirmed = Appointment.Create(
            TenantId, 10, 20, 30, From, From + Duration.FromMinutes(30), null, AppointmentCreatedBy.Staff, From);
        var cancelled = Appointment.Create(
            TenantId, 11, 21, 31, From, From + Duration.FromMinutes(30), null, AppointmentCreatedBy.Staff, From);
        cancelled.Cancel(From);

        _uow.Calls.Setup(c => c.SearchAsync(From, To, null, null, null, null, default)).ReturnsAsync([]);
        _uow.Appointments.Setup(a => a.GetForDateRangeAsync(From, To, null, default)).ReturnsAsync([confirmed, cancelled]);

        var result = await _sut.GetSummaryAsync(From, To, default);

        result.AppointmentsMade.ShouldBe(1);
    }

    /// <summary>Costs visible, so these tests are about the figures rather than about who may
    /// see them. A caller with no tenant of their own is the platform admin, and the platform
    /// admin sees everything — see CallCostVisibility, and CallCostVisibilityTests for the rule
    /// itself.</summary>
    private static CallCostVisibility CostsVisible(IUnitOfWork uow)
    {
        var platformAdmin = new Mock<ICurrentTenantProvider>();
        platformAdmin.SetupGet(t => t.TenantId).Returns((int?)null);
        return new CallCostVisibility(uow, platformAdmin.Object);
    }

}
