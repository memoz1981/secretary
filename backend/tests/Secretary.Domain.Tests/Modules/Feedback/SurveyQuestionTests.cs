using Secretary.Domain.Entities;
using Secretary.Domain.Enums;
using NodaTime;
using Shouldly;
using Xunit;

namespace Secretary.Domain.Tests;

/// <summary>A rating scale is a choice whose options carry numbers, and IsScored is the whole of
/// that idea. Everything the dashboard does with a question — average it or only count it — turns
/// on this one property, so it is worth pinning rather than trusting.</summary>
public sealed class SurveyQuestionTests
{
    private static readonly Instant Now = Instant.FromUnixTimeSeconds(1_754_000_000);

    private static SurveyQuestion Choice(bool headline = false)
        => SurveyQuestion.Create(1, 0, "Bizi qiymətləndirin", FeedbackQuestionType.Choice, headline, Now);

    [Fact]
    public void A_question_whose_options_all_carry_numbers_can_be_averaged()
    {
        var question = Choice();
        for (var value = 1; value <= 5; value++)
        {
            question.AddOption(value.ToString(), value, Now);
        }

        question.IsScored.ShouldBeTrue();
    }

    /// <summary>"Yaxşı" and "Pis" are countable and nothing more. A mean over them would be a
    /// number with no meaning, which is worse than no number.</summary>
    [Fact]
    public void A_question_with_bare_options_is_counted_not_averaged()
    {
        var question = Choice();
        question.AddOption("Yaxşı", null, Now);
        question.AddOption("Pis", null, Now);

        question.IsScored.ShouldBeFalse();
    }

    /// <summary>The case that would quietly lie: average the numbered answers, drop the rest, and
    /// report a mean over a subset nobody asked for.</summary>
    [Fact]
    public void One_unnumbered_option_makes_the_whole_question_unaverageable()
    {
        var question = Choice();
        question.AddOption("1", 1, Now);
        question.AddOption("2", 2, Now);
        question.AddOption("Bilmirəm", null, Now);

        question.IsScored.ShouldBeFalse();
    }

    [Fact]
    public void An_open_question_is_never_scored_and_takes_no_options()
    {
        var question = SurveyQuestion.Create(
            1, 0, "Nəyi yaxşılaşdıra bilərdik?", FeedbackQuestionType.Open, isHeadline: false, Now);

        question.IsScored.ShouldBeFalse();
        Should.Throw<InvalidOperationException>(() => question.AddOption("Bir şey", null, Now));
    }

    /// <summary>The headline is the one number a manager reads first, so it has to be a number.
    /// An open answer has none.</summary>
    [Fact]
    public void An_open_question_cannot_be_the_headline()
        => Should.Throw<ArgumentException>(() => SurveyQuestion.Create(
            1, 0, "Nə deyərdiniz?", FeedbackQuestionType.Open, isHeadline: true, Now));

    [Fact]
    public void A_question_needs_something_to_ask()
        => Should.Throw<ArgumentException>(() => SurveyQuestion.Create(
            1, 0, "   ", FeedbackQuestionType.Choice, isHeadline: false, Now));
}
