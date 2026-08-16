using Secretary.Domain.Abstractions;
using NodaTime;

namespace Secretary.Domain.Entities;

/// <summary>One answer a caller can pick.
///
/// ⚠ The score is a percentage, and it is derived by the question rather than typed by anybody.
/// The field it replaces was a free number box, and a real questionnaire used it for 20/40/60/80/100
/// while another was auto-numbered into Bəli=1, Xeyr=2. Both are legitimate readings of "a number
/// on an option", which is why the box had to go rather than be documented.
///
/// A percentage also makes questions commensurable: a Yes/No and a 1–5 both answer "how well did
/// this go, out of 100", so a questionnaire can have one score without weighting anything.</summary>
public sealed class SurveyQuestionOption : BaseEntity
{
    public int SurveyQuestionId { get; private set; }
    public int Position { get; private set; }
    public string Text { get; private set; }

    /// <summary>0–100, or null for an option with no ordering — every Choice option. Null means
    /// countable but not averageable, and the dashboard shows counts for those.</summary>
    public decimal? ScorePercent { get; private set; }

    /// <summary>The "Digər" option on a Choice question. Picked like any other, but the answer
    /// also carries what the caller actually said, because the whole point of offering it is that
    /// the list was incomplete.</summary>
    public bool IsOther { get; private set; }

    private SurveyQuestionOption() => Text = string.Empty;

    internal static SurveyQuestionOption Create(
        int questionId, int position, string text, decimal? scorePercent, bool isOther, Instant now)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("An option needs a label the caller can say.", nameof(text));
        }

        if (scorePercent is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(scorePercent), scorePercent, "A score is a percentage between 0 and 100.");
        }

        var option = new SurveyQuestionOption
        {
            SurveyQuestionId = questionId,
            Position = position,
            Text = text.Trim(),
            ScorePercent = scorePercent,
            IsOther = isOther,
        };

        option.InitBase(now);
        return option;
    }
}
