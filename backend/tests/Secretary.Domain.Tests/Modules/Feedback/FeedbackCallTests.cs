using Secretary.Domain.Entities;
using Secretary.Domain.Enums;
using Secretary.Domain.ValueObjects;
using NodaTime;
using Shouldly;
using Xunit;

namespace Secretary.Domain.Tests;

/// <summary>The status of a survey call is what the whole agent-quality half of the dashboard is
/// built on, and the distinction that carries it is Completed versus Abandoned.</summary>
public sealed class FeedbackCallTests
{
    private static readonly Instant Now = Instant.FromUnixTimeSeconds(1_754_000_000);

    private static FeedbackCall Queued()
        => FeedbackCall.Queue(1, 2, "Mehdi", "+994502505832", Now);

    [Fact]
    public void A_queued_call_has_not_happened_yet()
    {
        var call = Queued();

        call.CallStatus.ShouldBe(FeedbackCallStatus.Created);
        call.CompletedAt.ShouldBeNull();
        call.DurationSeconds.ShouldBe(0);
    }

    /// <summary>A call that ended without reaching the last question is abandoned, and the
    /// answers given up to that point are kept — where it stopped is the useful part.</summary>
    [Fact]
    public void A_call_that_ends_before_the_last_question_is_abandoned()
    {
        var call = Queued();
        call.Begin(Now);

        call.Finish(30, 4, 3, null, "gemini", CallPipeline.GeminiLive_3_1, TokenUsage.Zero, 0.01m, Now);

        call.CallStatus.ShouldBe(FeedbackCallStatus.Abandoned);
    }

    /// <summary>⚠ The one that would be wrong the other way round: somebody who answered every
    /// question and then hung up during the thank-you has finished the survey. Finish must not
    /// downgrade them to abandoned, or the completion rate under-reports every good call.</summary>
    [Fact]
    public void Finishing_a_completed_call_does_not_downgrade_it()
    {
        var call = Queued();
        call.Begin(Now);
        call.Complete(Now);

        call.Finish(45, 6, 5, null, "gemini", CallPipeline.GeminiLive_3_1, TokenUsage.Zero, 0.02m, Now);

        call.CallStatus.ShouldBe(FeedbackCallStatus.Completed);
        call.CompletedAt.ShouldNotBeNull();
    }

    /// <summary>⚠ The same trap as the one above, on the outcome that has to survive teardown.
    ///
    /// The hand-over is decided mid-call, and the call then ends — so Finish runs afterwards,
    /// every time. If it overwrote the status the way it overwrites everything else, "a person
    /// will call you back" would be a promise made to the caller and to nobody in the database,
    /// and the row would sit on the retry list looking like a dropped line.</summary>
    [Fact]
    public void Finishing_does_not_lose_a_hand_over_to_a_person()
    {
        var call = Queued();
        call.Begin(Now);
        call.HandOverToHuman(Now);

        call.Finish(22, 3, 3, null, "gemini", CallPipeline.GeminiLive_3_1, TokenUsage.Zero, 0.01m, Now);

        call.CallStatus.ShouldBe(FeedbackCallStatus.NeedsHuman);
    }

    /// <summary>The other way round: a survey that got every answer is finished, whatever went
    /// wrong during the goodbye.</summary>
    [Fact]
    public void A_completed_survey_is_never_handed_over()
    {
        var call = Queued();
        call.Begin(Now);
        call.Complete(Now);

        call.HandOverToHuman(Now);

        call.CallStatus.ShouldBe(FeedbackCallStatus.Completed);
    }

    [Fact]
    public void A_call_needs_somebody_to_be_about_and_a_number_to_reach_them_on()
    {
        Should.Throw<ArgumentException>(() => FeedbackCall.Queue(1, 2, "  ", "+994502505832", Now));
        Should.Throw<ArgumentException>(() => FeedbackCall.Queue(1, 2, "Mehdi", "  ", Now));
    }

    [Fact]
    public void A_call_cannot_finish_with_negative_figures()
        => Should.Throw<ArgumentOutOfRangeException>(() => Queued().Finish(
            -1, 0, 0, null, "gemini", CallPipeline.GeminiLive_3_1, TokenUsage.Zero, 0m, Now));
}

/// <summary>Three states, and the third is not an absence — see FeedbackAnswer.</summary>
public sealed class FeedbackAnswerTests
{
    private static readonly Instant Now = Instant.FromUnixTimeSeconds(1_754_000_000);

    [Fact]
    public void A_declined_answer_is_recorded_rather_than_left_out()
    {
        var answer = FeedbackAnswer.Refused(1, 2, Now);

        answer.Declined.ShouldBeTrue();
        answer.Text.ShouldBeNull();
        answer.SurveyQuestionOptionId.ShouldBeNull();
    }

    /// <summary>An open answer with no words is a decline. Storing it as an empty answer would
    /// count somebody who said nothing as somebody who answered.</summary>
    [Fact]
    public void An_empty_open_answer_is_refused()
        => Should.Throw<ArgumentException>(() => FeedbackAnswer.Said(1, 2, "   ", Now));

    [Fact]
    public void An_open_answer_keeps_the_callers_own_words()
        => FeedbackAnswer.Said(1, 2, "  Gözləmə çox uzun idi  ", Now).Text.ShouldBe("Gözləmə çox uzun idi");
}
