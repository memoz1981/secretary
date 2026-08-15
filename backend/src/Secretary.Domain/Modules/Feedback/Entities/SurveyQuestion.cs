using Secretary.Domain.Abstractions;
using Secretary.Domain.Enums;
using NodaTime;

namespace Secretary.Domain.Entities;

/// <summary>One question, and where it comes in the running order.
///
/// Position rather than an implicit id order: the owner reorders questions on the page, and a
/// survey read in insertion order would silently ignore that.</summary>
public sealed class SurveyQuestion : BaseEntity
{
    public int SurveyId { get; private set; }
    public int Position { get; private set; }
    public string Text { get; private set; }
    public FeedbackQuestionType QuestionType { get; private set; }

    /// <summary>The one number the dashboard leads with — an overall rating, usually.
    ///
    /// Without it every question gets an equal panel and the page has no headline, which is the
    /// difference between a manager glancing at it and a manager reading all of it. Only a
    /// Choice question with valued options can be one, and only one per survey.</summary>
    public bool IsHeadline { get; private set; }

    private readonly List<SurveyQuestionOption> _options = [];

    /// <summary>Empty for an Open question. The aggregate owns them so a Choice question cannot
    /// be saved without anything to choose.</summary>
    public IReadOnlyList<SurveyQuestionOption> Options => _options;

    private SurveyQuestion() => Text = string.Empty;

    public static SurveyQuestion Create(
        int surveyId, int position, string text, FeedbackQuestionType questionType, bool isHeadline, Instant now)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("A question needs to say something.", nameof(text));
        }

        if (isHeadline && questionType != FeedbackQuestionType.Choice)
        {
            throw new ArgumentException(
                "Only a multiple-choice question can be the headline — an open answer has no number to lead with.",
                nameof(isHeadline));
        }

        var question = new SurveyQuestion
        {
            SurveyId = surveyId,
            Position = position,
            Text = text.Trim(),
            QuestionType = questionType,
            IsHeadline = isHeadline,
        };

        question.InitBase(now);
        return question;
    }

    public void AddOption(string text, int? value, Instant now)
    {
        if (QuestionType != FeedbackQuestionType.Choice)
        {
            throw new InvalidOperationException("An open question has no options to choose from.");
        }

        _options.Add(SurveyQuestionOption.Create(Id, _options.Count, text, value, now));
        Touch(now);
    }

    public void ClearOptions(Instant now)
    {
        _options.Clear();
        Touch(now);
    }

    public void Update(string text, int position, bool isHeadline, Instant now)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("A question needs to say something.", nameof(text));
        }

        Text = text.Trim();
        Position = position;
        IsHeadline = isHeadline;
        Touch(now);
    }

    /// <summary>Whether this question's answers can be averaged rather than only counted.
    ///
    /// True when every option carries a number — "1, 2, 3, 4, 5". False for "Yaxşı / Pis", where
    /// the honest summary is a count per option and an average would be invented.</summary>
    public bool IsScored => QuestionType == FeedbackQuestionType.Choice
                            && _options.Count > 0
                            && _options.TrueForAll(o => o.Value is not null);
}
