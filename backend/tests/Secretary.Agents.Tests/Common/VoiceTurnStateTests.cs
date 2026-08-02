using Secretary.Agents.Realtime;
using Shouldly;
using Xunit;

namespace Secretary.Agents.Tests;

/// <summary>The dead-air bugs all lived in this state machine: asking the model to speak while
/// a response was still active (rejected, caller hears nothing), asking twice for the same turn
/// (two replies talking over each other), or asking again during the caller's own sentence.
/// None of that is reachable through a WebSocket in a test, which is exactly why the rules live
/// in a type of their own.</summary>
public sealed class VoiceTurnStateTests
{
    [Fact]
    public void A_follow_up_is_not_offered_before_anything_has_asked_for_one()
    {
        var state = new VoiceTurnState();

        state.TryClaimFollowUp(out _).ShouldBeFalse();
    }

    [Fact]
    public void A_finished_tool_call_is_what_makes_a_follow_up_due()
    {
        var state = new VoiceTurnState();
        state.BeginResponse();
        state.BeginToolCall();
        state.EndResponse();

        state.EndToolCall().ShouldBe(0);

        state.TryClaimFollowUp(out _).ShouldBeTrue();
    }

    [Fact]
    public void The_reply_waits_for_the_response_that_asked_for_the_tool_to_finish()
    {
        var state = new VoiceTurnState();
        state.BeginResponse();
        state.BeginToolCall();

        // OpenAI reports the function call before the response is done, so the tool can finish
        // first. Asking now is the original bug: the response is still active.
        state.EndToolCall();
        state.TryClaimFollowUp(out var snapshot).ShouldBeFalse();
        snapshot.ResponseActive.ShouldBeTrue();

        state.EndResponse();
        state.TryClaimFollowUp(out _).ShouldBeTrue();
    }

    [Fact]
    public void The_reply_waits_for_every_tool_of_the_turn()
    {
        var state = new VoiceTurnState();
        state.BeginResponse();
        state.BeginToolCall();
        state.BeginToolCall();
        state.EndResponse();

        state.EndToolCall().ShouldBe(1);
        state.TryClaimFollowUp(out var snapshot).ShouldBeFalse();
        snapshot.PendingToolCalls.ShouldBe(1);

        state.EndToolCall().ShouldBe(0);
        state.TryClaimFollowUp(out _).ShouldBeTrue();
    }

    [Fact]
    public void Only_one_racer_can_claim_the_same_follow_up()
    {
        var state = new VoiceTurnState();
        state.BeginResponse();
        state.BeginToolCall();
        state.EndResponse();
        state.EndToolCall();

        state.TryClaimFollowUp(out _).ShouldBeTrue();
        state.TryClaimFollowUp(out _).ShouldBeFalse();
    }

    [Fact]
    public void Only_one_of_many_concurrent_racers_can_claim_the_follow_up()
    {
        var state = new VoiceTurnState();
        state.BeginResponse();
        state.BeginToolCall();
        state.EndResponse();
        state.EndToolCall();

        var claims = 0;
        Parallel.For(0, 64, index =>
        {
            if (state.TryClaimFollowUp(out _))
            {
                Interlocked.Increment(ref claims);
            }
        });

        claims.ShouldBe(1);
    }

    [Fact]
    public void Caller_speech_only_interrupts_when_a_response_is_actually_live()
    {
        var state = new VoiceTurnState();

        // Nothing is being generated, so there is nothing to cancel — cancelling anyway is what
        // produced the response_cancel_not_active error events.
        state.BeginCallerSpeech().ShouldBeFalse();

        state.EndCallerSpeech();
        state.BeginResponse();
        state.BeginCallerSpeech().ShouldBeTrue();

        // And only once: the second speech-started of the same barge-in has nothing left to cut.
        state.BeginCallerSpeech().ShouldBeFalse();
    }

    [Fact]
    public void A_silent_response_is_re_asked_when_nothing_has_moved_on()
    {
        var state = new VoiceTurnState();
        var generation = state.BeginResponse();
        state.EndResponse();

        state.CanRetrySilentResponse(generation).ShouldBeTrue();
    }

    [Fact]
    public void A_silent_response_is_not_re_asked_once_another_response_has_opened()
    {
        var state = new VoiceTurnState();
        var generation = state.BeginResponse();
        state.EndResponse();

        // OpenAI's own turn detection got there first while we were waiting.
        state.BeginResponse();

        state.CanRetrySilentResponse(generation).ShouldBeFalse();
    }

    [Fact]
    public void A_silent_response_is_not_re_asked_over_the_caller_talking()
    {
        var state = new VoiceTurnState();
        var generation = state.BeginResponse();
        state.EndResponse();
        state.BeginCallerSpeech();

        state.CanRetrySilentResponse(generation).ShouldBeFalse();

        state.EndCallerSpeech();
        state.CanRetrySilentResponse(generation).ShouldBeTrue();
    }

    [Fact]
    public void A_silent_response_is_not_re_asked_while_a_tool_is_still_running()
    {
        var state = new VoiceTurnState();
        var generation = state.BeginResponse();
        state.BeginToolCall();
        state.EndResponse();

        state.CanRetrySilentResponse(generation).ShouldBeFalse();
    }
}
