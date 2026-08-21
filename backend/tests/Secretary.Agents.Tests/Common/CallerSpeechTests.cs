using Shouldly;
using Xunit;

namespace Secretary.Agents.Tests;

/// <summary>The caller's words come from the transcription, never from the model's report of them.
///
/// ⚠ On a real survey call the caller said "Ömür əllərim belə çox gözləmədim. O yaxşı idi…" and
/// what was stored against them was "Ümumi rəylərim. Belə, çox gözləmədim. O yaxşı idi…" — a
/// plausible sentence nobody uttered, in the one field whose entire purpose is their own words.
/// The instruction said "pass on exactly what they said", in bold. It made no difference: tidying
/// text is what models do.</summary>
public sealed class CallerSpeechTests
{
    [Fact]
    public void Nothing_said_since_the_question_is_nothing_to_record()
    {
        var speech = new CallerSpeech();
        speech.Heard("Bəli, buyurun.");
        speech.StartOfAnswer();

        speech.SinceQuestion().ShouldBeNull();
    }

    [Fact]
    public void Only_what_came_after_the_question_is_the_answer()
    {
        var speech = new CallerSpeech();
        speech.Heard("Bəli, buyurun.");
        speech.StartOfAnswer();
        speech.Heard("səkkiz");

        speech.SinceQuestion().ShouldBe("səkkiz");
    }

    /// <summary>⚠ A pause ends a turn without ending a sentence. Somebody thinking aloud arrives
    /// in pieces, and taking only the first is how "çox gözləmədim" became the whole of an answer
    /// that went on to explain why.</summary>
    [Fact]
    public void Every_piece_of_a_rambling_answer_is_one_answer()
    {
        var speech = new CallerSpeech();
        speech.StartOfAnswer();
        speech.Heard("Ümumi rəylərim belədir ki,");
        speech.Heard("  servis çox yaxşıdır.  ");

        speech.SinceQuestion().ShouldBe("Ümumi rəylərim belədir ki, servis çox yaxşıdır.");
    }

    [Fact]
    public void The_next_question_starts_a_new_answer()
    {
        var speech = new CallerSpeech();
        speech.StartOfAnswer();
        speech.Heard("Bəli.");
        speech.StartOfAnswer();
        speech.Heard("səkkiz");

        speech.SinceQuestion().ShouldBe("səkkiz");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Silence_is_not_a_fragment(string? nothing)
    {
        var speech = new CallerSpeech();
        speech.StartOfAnswer();
        speech.Heard(nothing);

        speech.SinceQuestion().ShouldBeNull();
    }
}
