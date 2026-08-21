using Secretary.Domain.Enums;
using Secretary.Domain.Abstractions;
using NodaTime;

namespace Secretary.Domain.Entities;

/// <summary>One question, its shape, and where it comes in the running order.
///
/// ⚠ There is no general-purpose constructor, and that is the point. Each type builds its own
/// options and its own scores, so a Scale cannot be saved with hand-typed options and a Choice
/// cannot be saved with numbers behind its labels. The version this replaces had one constructor
/// and one editable number per option, and produced a live questionnaire scoring Bəli=1, Xeyr=2.
///
/// Position rather than an implicit id order: the owner reorders questions on the page, and a
/// survey read in insertion order would silently ignore that.</summary>
public sealed class SurveyQuestion : BaseEntity
{
    /// <summary>The two labels a Yes/No question always has. Azerbaijani because the agent is —
    /// this module has one instruction file and it is written in it.</summary>
    public const string Yes = "Bəli";
    public const string No = "Xeyr";

    /// <summary>The "Other" label, when a Choice question allows one.</summary>
    public const string Other = "Digər";

    /// <summary>A scale runs to one of these. Not free, because "1 to 7" is a scale nobody can
    /// hold in their head while listening, and every extra shape is a column on the dashboard.</summary>
    public static readonly IReadOnlyList<int> AllowedScaleMaximums = [3, 5, 10];

    public int SurveyId { get; private set; }
    public int Position { get; private set; }
    public string Text { get; private set; }
    public FeedbackQuestionType QuestionType { get; private set; }

    /// <summary>Scale only: the top of the scale. Null for every other type.</summary>
    public int? ScaleMax { get; private set; }

    /// <summary>Yes/No only. False when the good answer is "Xeyr" — "Gözləmə uzun oldu?" is a
    /// question you want answered no, and without this its score would run backwards.</summary>
    public bool YesIsPositive { get; private set; }

    /// <summary>Choice only: whether the list ends with "Digər", which also records what the
    /// caller said.</summary>
    public bool AllowOther { get; private set; }

    /// <summary>Whether this question's score feeds the questionnaire's one number.
    ///
    /// Replaces a single "headline" tick, and generalises it. "Məmnun qaldınız?" is satisfaction;
    /// "Servis kitabçası verildi mi?" is a fact about the process. Averaging them gives a number
    /// that moves for two unrelated reasons, and nobody can tell which.</summary>
    public bool CountsTowardScore { get; private set; }

    private readonly List<SurveyQuestionOption> _options = [];

    /// <summary>Empty for an Open question. The aggregate owns them, so they cannot disagree with
    /// the type.</summary>
    public IReadOnlyList<SurveyQuestionOption> Options => _options;

    private SurveyQuestion() => Text = string.Empty;

    /// <summary>Whether the answers to this question can be averaged rather than only counted.
    ///
    /// Read off the type now, not off "do all the options happen to have numbers". The old
    /// derivation was true by accident whenever somebody filled every value box in.</summary>
    public bool IsScored => QuestionType is FeedbackQuestionType.YesNo or FeedbackQuestionType.Scale;

    // ---- Building one ----

    public static SurveyQuestion YesNoQuestion(
        int surveyId, int position, string text, bool yesIsPositive, bool countsTowardScore, Instant now)
    {
        var question = New(surveyId, position, text, FeedbackQuestionType.YesNo, now);
        question.YesIsPositive = yesIsPositive;
        question.CountsTowardScore = countsTowardScore;
        question.BuildOptions(now);
        return question;
    }

    public static SurveyQuestion ScaleQuestion(
        int surveyId, int position, string text, int scaleMax, bool countsTowardScore, Instant now)
    {
        var question = New(surveyId, position, text, FeedbackQuestionType.Scale, now);
        question.ScaleMax = RequireAllowedScale(scaleMax);
        question.CountsTowardScore = countsTowardScore;
        question.BuildOptions(now);
        return question;
    }

    public static SurveyQuestion ChoiceQuestion(
        int surveyId, int position, string text, IReadOnlyList<string> labels, bool allowOther, Instant now)
    {
        var question = New(surveyId, position, text, FeedbackQuestionType.Choice, now);
        question.AllowOther = allowOther;
        question.BuildOptions(now, labels);
        return question;
    }

    public static SurveyQuestion OpenQuestion(int surveyId, int position, string text, Instant now)
        => New(surveyId, position, text, FeedbackQuestionType.Open, now);

    // ---- Changing one ----

    /// <summary>Rewording, and nothing else. Leaves the options exactly where they are, which is
    /// what makes it safe on a question people have already answered.</summary>
    public void Retitle(string text, bool countsTowardScore, Instant now)
    {
        Text = RequireText(text);
        CountsTowardScore = countsTowardScore && IsScored;
        Touch(now);
    }

    /// <summary>Re-shapes the question in place, options and all.
    ///
    /// The options are rebuilt rather than merged, which would orphan any answer pointing at one.
    /// The service refuses this outright once a caller has answered — see SurveyService.</summary>
    public void Reshape(
        FeedbackQuestionType type, string text, bool countsTowardScore,
        int? scaleMax, bool yesIsPositive, bool allowOther, IReadOnlyList<string> labels, Instant now)
    {
        Text = RequireText(text);
        QuestionType = type;
        ScaleMax = type == FeedbackQuestionType.Scale ? RequireAllowedScale(scaleMax ?? 0) : null;
        YesIsPositive = type == FeedbackQuestionType.YesNo && yesIsPositive;
        AllowOther = type == FeedbackQuestionType.Choice && allowOther;
        CountsTowardScore = countsTowardScore && IsScored;

        _options.Clear();
        BuildOptions(now, labels);
        Touch(now);
    }

    public void MoveTo(int position, Instant now)
    {
        Position = position;
        Touch(now);
    }

    // ---- The scores, derived here and nowhere else ----

    private void BuildOptions(Instant now, IReadOnlyList<string>? labels = null)
    {
        switch (QuestionType)
        {
            case FeedbackQuestionType.YesNo:
                Add(Yes, YesIsPositive ? 100m : 0m, now);
                Add(No, YesIsPositive ? 0m : 100m, now);
                break;

            case FeedbackQuestionType.Scale:
                var top = ScaleMax!.Value;

                // (i-1)/(N-1), so 1 is 0% and N is 100%. Dividing by N instead would floor a 1-5
                // at 20%, and a questionnaire everybody hated would read as one fifth satisfied.
                for (var i = 1; i <= top; i++)
                {
                    Add(i.ToString(), Math.Round((i - 1) * 100m / (top - 1), 2), now);
                }

                break;

            case FeedbackQuestionType.Choice:
                var given = (labels ?? []).Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
                if (given.Count < 2)
                {
                    throw new ArgumentException("A choice needs at least two options.", nameof(labels));
                }

                foreach (var label in given)
                {
                    Add(label, null, now);
                }

                if (AllowOther)
                {
                    Add(Other, null, now, isOther: true);
                }

                break;

            case FeedbackQuestionType.Open:
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(QuestionType), QuestionType, "Unknown question type.");
        }
    }

    private void Add(string text, decimal? score, Instant now, bool isOther = false)
        => _options.Add(SurveyQuestionOption.Create(Id, _options.Count, text, score, isOther, now));

    // ---- Guards ----

    private static SurveyQuestion New(
        int surveyId, int position, string text, FeedbackQuestionType type, Instant now)
    {
        var question = new SurveyQuestion
        {
            SurveyId = surveyId,
            Position = position,
            Text = RequireText(text),
            QuestionType = type,
        };

        question.InitBase(now);
        return question;
    }

    private static string RequireText(string text)
        => string.IsNullOrWhiteSpace(text)
            ? throw new ArgumentException("A question needs to say something.", nameof(text))
            : text.Trim();

    private static int RequireAllowedScale(int scaleMax)
        => AllowedScaleMaximums.Contains(scaleMax)
            ? scaleMax
            : throw new ArgumentOutOfRangeException(
                nameof(scaleMax), scaleMax,
                $"A scale runs to {string.Join(", ", AllowedScaleMaximums)} — nothing else.");
}
