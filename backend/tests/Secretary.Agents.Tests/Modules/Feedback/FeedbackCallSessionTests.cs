using Secretary.Agents.Feedback;
using Shouldly;
using Xunit;

namespace Secretary.Agents.Tests;

/// <summary>A survey that cannot get past a question is worse than one missing an answer.
///
/// On a real call the caller answered "dörd", "beş", "üç" and every one was refused, so the agent
/// read the same five options back five times before they gave up. The matcher was at fault that
/// day — but no matcher will ever be right about everything anybody says, so the loop needs an end
/// that does not depend on one being right.</summary>
public sealed class FeedbackCallSessionTests
{
    [Fact]
    public void A_question_is_given_two_goes_before_the_survey_moves_on()
    {
        var session = new FeedbackCallSession();

        session.TooManyFailuresFor(7).ShouldBeFalse();
        session.TooManyFailuresFor(7).ShouldBeTrue();
    }

    /// <summary>Counted per question, not per call: two misheard answers early must not make the
    /// rest of the survey give up on its first try.</summary>
    [Fact]
    public void Each_question_gets_its_own_allowance()
    {
        var session = new FeedbackCallSession();

        session.TooManyFailuresFor(7).ShouldBeFalse();
        session.TooManyFailuresFor(7).ShouldBeTrue();

        session.TooManyFailuresFor(8).ShouldBeFalse();
    }

    /// <summary>Answers filed against no call, or the wrong one, would appear under another
    /// person's name — so a tool with no id refuses rather than guessing.</summary>
    [Fact]
    public void A_call_that_was_never_queued_refuses_rather_than_guessing()
        => Should.Throw<InvalidOperationException>(() => new FeedbackCallSession().Require());
}
