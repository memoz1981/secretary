using Secretary.Domain.Abstractions;
using NodaTime;

namespace Secretary.Domain.Entities;

/// <summary>One answer a caller can pick.
///
/// Value is what makes a rating scale unnecessary as a separate question type. "1" through "5"
/// are five options carrying 1–5, so they average; "Yaxşı" and "Pis" carry nothing, so they only
/// count. One shape, and the dashboard reads the difference off the data rather than off a type
/// flag somebody has to keep in sync.</summary>
public sealed class SurveyQuestionOption : BaseEntity
{
    public int SurveyQuestionId { get; private set; }
    public int Position { get; private set; }
    public string Text { get; private set; }

    /// <summary>The number behind the words, when there is one. Null means countable but not
    /// averageable.</summary>
    public int? Value { get; private set; }

    private SurveyQuestionOption() => Text = string.Empty;

    public static SurveyQuestionOption Create(int questionId, int position, string text, int? value, Instant now)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("An option needs a label the caller can say.", nameof(text));
        }

        var option = new SurveyQuestionOption
        {
            SurveyQuestionId = questionId,
            Position = position,
            Text = text.Trim(),
            Value = value,
        };

        option.InitBase(now);
        return option;
    }
}
