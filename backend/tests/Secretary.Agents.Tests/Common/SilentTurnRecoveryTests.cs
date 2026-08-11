using Secretary.Agents.Realtime;
using Shouldly;
using Xunit;

namespace Secretary.Agents.Tests;

/// <summary>When a turn makes no sound, whether we re-ask is the difference between a pause and
/// a call that never recovers. The rule lives outside the WebSocket loop for the same reason
/// VoiceTurnState does: every version of this bug has been unreachable from a test.</summary>
public sealed class SilentTurnRecoveryTests
{
    private const bool ProviderResumes = true;      // Gemini: the turn continues after a tool result
    private const bool WeResume = false;            // OpenAI: the follow-up is ours to start

    /// <summary>The fourteen-second hole. A caller was identified, Gemini reported the turn
    /// interrupted 310 ms after the tool result having said nothing, and nothing spoke again
    /// until the caller said "Aló. Aló." — because a tool call disqualified recovery outright.</summary>
    [Fact]
    public void A_silent_tool_turn_is_re_asked_when_the_provider_owns_the_follow_up()
        => LiveVoiceCallOrchestrator.ShouldReAskAfterSilence(
            toolCallInResponse: true, ProviderResumes, statusReason: "interrupted", retriesSoFar: 0)
            .ShouldBeTrue();

    /// <summary>The case the old rule was written for, and it is still right: our own follow-up
    /// is already on its way, so re-asking would put two replies on the line at once.</summary>
    [Fact]
    public void A_silent_tool_turn_is_left_alone_when_the_follow_up_is_ours_to_start()
        => LiveVoiceCallOrchestrator.ShouldReAskAfterSilence(
            toolCallInResponse: true, WeResume, statusReason: null, retriesSoFar: 0)
            .ShouldBeFalse();

    /// <summary>Silence the caller caused is not silence to fix — their own turn produces the
    /// next response, and re-asking over it is what produced bursts of doomed 50 ms replies
    /// while they were still talking.</summary>
    [Theory]
    [InlineData("turn_detected")]
    [InlineData("TurnDetected")]
    [InlineData("client_cancelled")]
    public void Silence_that_was_meant_is_left_alone(string reason)
        => LiveVoiceCallOrchestrator.ShouldReAskAfterSilence(
            toolCallInResponse: false, ProviderResumes, reason, retriesSoFar: 0)
            .ShouldBeFalse();

    [Fact]
    public void An_unexplained_silent_turn_is_still_re_asked()
        => LiveVoiceCallOrchestrator.ShouldReAskAfterSilence(
            toolCallInResponse: false, WeResume, statusReason: null, retriesSoFar: 0)
            .ShouldBeTrue();

    /// <summary>Two attempts and then it stops. A retry that keeps failing is a caller listening
    /// to nothing for longer, not a caller being rescued.</summary>
    [Fact]
    public void Re_asking_gives_up_rather_than_looping()
    {
        LiveVoiceCallOrchestrator.ShouldReAskAfterSilence(true, ProviderResumes, "interrupted", 1)
            .ShouldBeTrue();

        LiveVoiceCallOrchestrator.ShouldReAskAfterSilence(true, ProviderResumes, "interrupted", 2)
            .ShouldBeFalse();
    }
}
