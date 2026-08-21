using Secretary.Domain.Entities;
using Secretary.Domain.Enums;
using NodaTime;
using Shouldly;
using Xunit;

namespace Secretary.Domain.Tests;

/// <summary>One person to be surveyed, and every dial against them.
///
/// ⚠ This type exists because the two were the same row. Four dead connections in nine seconds on
/// 15 August appeared as four calls beside one real survey, and "how many people did we survey"
/// was answered with "how many times did we press the button".
///
/// The retry rule is the part worth pinning: it decides who gets rung again, and getting it wrong
/// in the generous direction means ringing a private individual over and over.</summary>
public sealed class SurveyRequestTests
{
    private static readonly Instant Now = Instant.FromUnixTimeSeconds(1_754_000_000);

    private static SurveyRequest Queued() => SurveyRequest.Queue(1, 2, "Mehdi", "+994501112233", Now);

    /// <summary>A business open around the clock, so these tests are about the retry rule and not
    /// about opening hours. CallingHoursTests covers the clamp.</summary>
    private static readonly Func<Instant, Instant?> AlwaysOpen = due => due;

    [Fact]
    public void A_queued_request_is_due_immediately_and_has_not_been_tried()
    {
        var request = Queued();

        request.Outcome.ShouldBe(SurveyRequestOutcome.Pending);
        request.AttemptCount.ShouldBe(0);
        request.NextAttemptDueAt.ShouldBe(Now);
    }

    /// <summary>⚠ Counted before the dial, not after. Counting afterwards fails open: a dial that
    /// never reports back leaves the count untouched and the request eligible forever.</summary>
    [Fact]
    public void An_attempt_counts_the_moment_it_begins()
    {
        var request = Queued();

        request.BeginAttempt(Now);

        request.AttemptCount.ShouldBe(1);
        request.LastAttemptAt.ShouldBe(Now);
        request.NextAttemptDueAt.ShouldBeNull();
    }

    [Fact]
    public void Nobody_home_is_tried_again_after_the_delay()
    {
        var request = Queued();
        request.BeginAttempt(Now);

        request.Settle(SurveyRequestOutcome.NotReached, retryCount: 2, retryDelayMinutes: 60, AlwaysOpen, Now);

        request.NextAttemptDueAt.ShouldBe(Now.Plus(Duration.FromMinutes(60)));
        request.NeedsFollowUp.ShouldBeFalse();
    }

    [Fact]
    public void The_retries_run_out()
    {
        var request = Queued();

        for (var attempt = 1; attempt <= 3; attempt++)
        {
            request.BeginAttempt(Now);
            request.Settle(SurveyRequestOutcome.NotReached, retryCount: 2, retryDelayMinutes: 60, AlwaysOpen, Now);
        }

        request.AttemptCount.ShouldBe(3);
        request.NextAttemptDueAt.ShouldBeNull();

        // And now it is a person's problem rather than a number nobody will ever ring.
        request.NeedsFollowUp.ShouldBeTrue();
    }

    /// <summary>⚠ The three outcomes that must never be dialled again, and the reasons differ.
    ///
    /// Refused is an answer — the worst one, but an answer, and ringing back somebody who said no
    /// is how a number gets blocked. NeedsHuman would fail identically on a second attempt with
    /// the same agent. Complete is done.</summary>
    [Theory]
    [InlineData(SurveyRequestOutcome.Refused)]
    [InlineData(SurveyRequestOutcome.NeedsHuman)]
    [InlineData(SurveyRequestOutcome.Complete)]
    public void Anybody_who_answered_is_never_rung_again(SurveyRequestOutcome outcome)
    {
        var request = Queued();
        request.BeginAttempt(Now);

        request.Settle(outcome, retryCount: 5, retryDelayMinutes: 60, AlwaysOpen, Now);

        request.NextAttemptDueAt.ShouldBeNull();
    }

    /// <summary>A person still has to ring the one that broke down. A refusal needs nobody, and
    /// putting it on the follow-up list would bury the rows that do.</summary>
    [Theory]
    [InlineData(SurveyRequestOutcome.NeedsHuman, true)]
    [InlineData(SurveyRequestOutcome.Refused, false)]
    [InlineData(SurveyRequestOutcome.Complete, false)]
    public void Only_a_breakdown_or_an_unreachable_number_needs_a_person(
        SurveyRequestOutcome outcome, bool expected)
    {
        var request = Queued();
        request.BeginAttempt(Now);
        request.Settle(outcome, retryCount: 0, retryDelayMinutes: 60, AlwaysOpen, Now);

        request.NeedsFollowUp.ShouldBe(expected);
    }

    /// <summary>Turning the retries off means it for the people already waiting. The policy is
    /// read at the moment it is applied rather than copied onto the request when it was queued.</summary>
    [Fact]
    public void No_retries_configured_means_one_attempt()
    {
        var request = Queued();
        request.BeginAttempt(Now);

        request.Settle(SurveyRequestOutcome.NotReached, retryCount: 0, retryDelayMinutes: 60, AlwaysOpen, Now);

        request.NextAttemptDueAt.ShouldBeNull();
    }

    [Fact]
    public void Closing_it_by_hand_stops_the_chasing()
    {
        var request = Queued();
        request.BeginAttempt(Now);
        request.Settle(SurveyRequestOutcome.NotReached, retryCount: 3, retryDelayMinutes: 60, AlwaysOpen, Now);

        request.CloseWithoutAnswer(Now);

        request.NextAttemptDueAt.ShouldBeNull();
        request.Outcome.ShouldBe(SurveyRequestOutcome.NotReached);
    }

    [Fact]
    public void A_request_needs_somebody_to_be_about_and_a_number_to_reach_them_on()
    {
        Should.Throw<ArgumentException>(() => SurveyRequest.Queue(1, 2, "  ", "+994501112233", Now));
        Should.Throw<ArgumentException>(() => SurveyRequest.Queue(1, 2, "Mehdi", "  ", Now));
    }
}

/// <summary>The retry policy is bounded rather than free. An unbounded count is a number somebody
/// types wrong once and a customer is rung fifty times.</summary>
public sealed class SurveyRetryPolicyTests
{
    private static readonly Instant Now = Instant.FromUnixTimeSeconds(1_754_000_000);

    [Fact]
    public void A_new_questionnaire_does_not_chase_anybody()
    {
        var survey = Survey.Create(1, "Servis rəyi", Now);

        survey.RetryCount.ShouldBe(0);
        survey.RetryDelayMinutes.ShouldBe(Survey.DefaultRetryDelayMinutes);
    }

    [Theory]
    [InlineData(-1, 60)]
    [InlineData(6, 60)]
    [InlineData(1, 5)]
    [InlineData(1, 60 * 24 * 30)]
    public void A_policy_outside_the_bounds_is_refused(int retryCount, int delayMinutes)
        => Should.Throw<ArgumentOutOfRangeException>(
            () => Survey.Create(1, "Servis rəyi", Now).SetRetryPolicy(retryCount, delayMinutes, Now));
}
