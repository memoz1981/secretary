using Secretary.Agents.Realtime;
using NodaTime;
using NodaTime.Text;
using Shouldly;
using Xunit;

namespace Secretary.Agents.Tests;

/// <summary>The caller's words, with the clock time each was said at.
///
/// ⚠ This column existed on three tables for months and was written null on every call, because
/// the value was hardcoded at the single place a CallLogEntry is built. Nothing failed — an
/// unwritten nullable column looks exactly like a caller who said nothing — and the Feedback
/// module was designed on top of it, with open answers meant to come from a transcript that was
/// never there.</summary>
public sealed class CallerTranscriptTests
{
    private static Instant Baku(string local)
        => LocalDateTimePattern.CreateWithInvariantCulture("yyyy-MM-dd HH:mm:ss")
            .Parse(local).Value
            .InZoneStrictly(AzerbaijanTime.Zone)
            .ToInstant();

    [Fact]
    public void A_line_says_the_time_it_was_said_at()
        => LiveVoiceCallOrchestrator.CallerLine(Baku("2026-08-16 13:50:04"), "Bəli")
            .ShouldBe("[13:50:04] Bəli");

    /// <summary>⚠ Azerbaijan local time, not UTC. The instant below is 09:50 UTC; a transcript
    /// showing that would put every call four hours before it happened, and quietly disagree with
    /// the call's own timestamp printed at the top of the same page.</summary>
    [Fact]
    public void The_time_is_local_and_not_utc()
        => LiveVoiceCallOrchestrator.CallerLine(Instant.FromUtc(2026, 8, 16, 9, 50, 4), "Bəli")
            .ShouldBe("[13:50:04] Bəli");

    [Fact]
    public void The_words_are_trimmed_but_otherwise_untouched()
        => LiveVoiceCallOrchestrator.CallerLine(Baku("2026-08-16 09:00:00"), "  Gözləmə çox uzun idi  ")
            .ShouldBe("[09:00:00] Gözləmə çox uzun idi");
}
