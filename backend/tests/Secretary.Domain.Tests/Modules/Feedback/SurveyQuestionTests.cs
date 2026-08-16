using Secretary.Domain.Entities;
using Secretary.Domain.Enums;
using NodaTime;
using Shouldly;
using Xunit;

namespace Secretary.Domain.Tests;

/// <summary>Each question type builds its own options and its own scores, and nothing numeric is
/// typed by anybody.
///
/// ⚠ These tests exist because the version they replace let the owner type a number per option.
/// One live questionnaire used it for 20/40/60/80/100 and another was auto-numbered into Bəli=1,
/// Xeyr=2 — so "no" outscored "yes" on a satisfaction question. Both were legitimate uses of a
/// free number box, which is why the box is gone rather than documented.</summary>
public sealed class SurveyQuestionTests
{
    private static readonly Instant Now = Instant.FromUnixTimeSeconds(1_754_000_000);

    // ---- Yes/No ----

    [Fact]
    public void Yes_no_builds_its_two_options_and_scores_yes_full_marks()
    {
        var question = SurveyQuestion.YesNoQuestion(1, 0, "Məmnun qaldınız?", true, true, Now);

        question.Options.Select(o => (o.Text, o.ScorePercent))
            .ShouldBe([("Bəli", 100m), ("Xeyr", 0m)]);
        question.IsScored.ShouldBeTrue();
    }

    /// <summary>"Gözləmə uzun oldu?" is a question you want answered no. Without this the score
    /// runs backwards and a well-run service reads as a failing one.</summary>
    [Fact]
    public void Yes_no_scores_the_other_way_when_no_is_the_good_answer()
    {
        var question = SurveyQuestion.YesNoQuestion(1, 0, "Gözləmə uzun oldu?", false, true, Now);

        question.Options.Select(o => (o.Text, o.ScorePercent))
            .ShouldBe([("Bəli", 0m), ("Xeyr", 100m)]);
    }

    // ---- Scale ----

    /// <summary>1 is 0% and N is 100%, so a Scale and a Yes/No mean the same thing by the same
    /// measure and the questionnaire can have one score without weighting anything.</summary>
    [Fact]
    public void A_five_point_scale_runs_from_nought_to_a_hundred()
    {
        var question = SurveyQuestion.ScaleQuestion(1, 0, "1-dən 5-ə qədər", 5, true, Now);

        question.Options.Select(o => o.Text).ShouldBe(["1", "2", "3", "4", "5"]);
        question.Options.Select(o => o.ScorePercent).ShouldBe([0m, 25m, 50m, 75m, 100m]);
    }

    /// <summary>⚠ Dividing by N instead of N-1 would floor a 1-5 at 20%, and a questionnaire
    /// everybody hated would report as a fifth satisfied. Ten steps also do not land on whole
    /// numbers, which is why the score is stored with two decimal places.</summary>
    [Fact]
    public void A_ten_point_scale_starts_at_nought_and_keeps_its_fractions()
    {
        var question = SurveyQuestion.ScaleQuestion(1, 0, "1-dən 10-a qədər", 10, true, Now);

        question.Options[0].ScorePercent.ShouldBe(0m);
        question.Options[1].ScorePercent.ShouldBe(11.11m);
        question.Options[9].ScorePercent.ShouldBe(100m);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(7)]
    [InlineData(100)]
    public void A_scale_runs_to_three_five_or_ten_and_nothing_else(int scaleMax)
        => Should.Throw<ArgumentOutOfRangeException>(
            () => SurveyQuestion.ScaleQuestion(1, 0, "Qiymət", scaleMax, true, Now));

    // ---- Choice ----

    /// <summary>"Təmir" and "Satış" have no order between them, so there is nothing to average and
    /// no score to invent.</summary>
    [Fact]
    public void A_choice_carries_labels_and_no_scores_at_all()
    {
        var question = SurveyQuestion.ChoiceQuestion(1, 0, "Nə üçün gəldiniz?", ["Təmir", "Satış"], false, Now);

        question.Options.Select(o => o.Text).ShouldBe(["Təmir", "Satış"]);
        question.Options.ShouldAllBe(o => o.ScorePercent == null);
        question.IsScored.ShouldBeFalse();
    }

    [Fact]
    public void Allowing_other_appends_it_last_and_marks_it()
    {
        var question = SurveyQuestion.ChoiceQuestion(1, 0, "Nə üçün gəldiniz?", ["Təmir", "Satış"], true, Now);

        question.Options[^1].Text.ShouldBe("Digər");
        question.Options[^1].IsOther.ShouldBeTrue();
        question.Options.Count(o => o.IsOther).ShouldBe(1);
    }

    [Fact]
    public void A_choice_needs_something_to_choose_between()
        => Should.Throw<ArgumentException>(
            () => SurveyQuestion.ChoiceQuestion(1, 0, "Nə üçün?", ["Təmir"], false, Now));

    // ---- Open ----

    [Fact]
    public void An_open_question_has_no_options_and_no_score()
    {
        var question = SurveyQuestion.OpenQuestion(1, 0, "Nəyi yaxşılaşdıra bilərdik?", Now);

        question.Options.ShouldBeEmpty();
        question.IsScored.ShouldBeFalse();
    }

    // ---- The score tick ----

    /// <summary>A tick on a question with no score would put it in the average as a zero, or drop
    /// it silently. Refusing it in the entity means the API cannot be talked into either.</summary>
    [Fact]
    public void Only_a_scored_question_can_count_toward_the_score()
    {
        var choice = SurveyQuestion.ChoiceQuestion(1, 0, "Nə üçün?", ["Təmir", "Satış"], false, Now);
        choice.Retitle("Nə üçün gəldiniz?", countsTowardScore: true, Now);

        choice.CountsTowardScore.ShouldBeFalse();
    }

    // ---- Editing ----

    /// <summary>⚠ Retitle must not touch the options. Rebuilding them for a typo fix would hand
    /// every option a new id and orphan the answers pointing at the old ones — a silent version of
    /// the deletion the service refuses outright.</summary>
    [Fact]
    public void Rewording_leaves_the_options_exactly_where_they_were()
    {
        var question = SurveyQuestion.ScaleQuestion(1, 0, "1-dən 5-ə", 5, true, Now);
        var before = question.Options.ToList();

        question.Retitle("1-dən 5-ə qədər neçə bal verərdiniz?", true, Now);

        question.Options.ShouldBe(before);
    }

    [Fact]
    public void Reshaping_rebuilds_the_options_for_the_new_type()
    {
        var question = SurveyQuestion.ScaleQuestion(1, 0, "1-dən 5-ə", 5, true, Now);

        question.Reshape(
            FeedbackQuestionType.YesNo, "Məmnun qaldınız?", true,
            scaleMax: null, yesIsPositive: true, allowOther: false, labels: [], Now);

        question.ScaleMax.ShouldBeNull();
        question.Options.Select(o => o.Text).ShouldBe(["Bəli", "Xeyr"]);
    }

    [Fact]
    public void A_question_needs_something_to_ask()
        => Should.Throw<ArgumentException>(() => SurveyQuestion.OpenQuestion(1, 0, "   ", Now));
}
