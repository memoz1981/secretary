using Secretary.Agents.Realtime;
using NodaTime;
using Shouldly;
using Xunit;

namespace Secretary.Agents.Tests;

/// <summary>The caller's words, timestamped from the start of the call.
///
/// ⚠ This column existed on three tables for months and was written null on every call, because
/// the value was hardcoded at the single place a CallLogEntry is built. Nothing failed — an
/// unwritten nullable column looks exactly like a caller who said nothing — and the Feedback
/// module was designed on top of it, with open answers meant to come from a transcript that was
/// never there.</summary>
public sealed class CallerTranscriptTests
{
    [Theory]
    [InlineData(0, "[00:00]")]
    [InlineData(9, "[00:09]")]
    [InlineData(75, "[01:15]")]
    public void A_line_says_how_far_into_the_call_it_was_said(int seconds, string expected)
        => LiveVoiceCallOrchestrator.CallerLine(Duration.FromSeconds(seconds), "Bəli")
            .ShouldBe($"{expected} Bəli");

    /// <summary>Minutes come from the total, not from a minutes component. A component would wrap
    /// at the hour and put the end of a long call before the middle of it.</summary>
    [Fact]
    public void A_call_past_the_hour_keeps_counting_up()
        => LiveVoiceCallOrchestrator.CallerLine(Duration.FromSeconds(3700), "Bəli")
            .ShouldStartWith("[61:40]");

    /// <summary>Clock skew between the start instant and a transcript should not produce a
    /// negative timestamp that sorts above everything.</summary>
    [Fact]
    public void A_line_from_before_the_start_is_clamped_rather_than_negative()
        => LiveVoiceCallOrchestrator.CallerLine(Duration.FromSeconds(-5), "Bəli")
            .ShouldStartWith("[00:00]");

    [Fact]
    public void The_words_are_trimmed_but_otherwise_untouched()
        => LiveVoiceCallOrchestrator.CallerLine(Duration.Zero, "  Gözləmə çox uzun idi  ")
            .ShouldBe("[00:00] Gözləmə çox uzun idi");
}
