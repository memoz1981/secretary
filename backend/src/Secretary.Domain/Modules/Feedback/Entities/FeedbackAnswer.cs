using Secretary.Domain.Abstractions;
using NodaTime;

namespace Secretary.Domain.Entities;

/// <summary>What one person said to one question.
///
/// Three states, and the third is not an absence. Answered-with-an-option, answered-in-words, or
/// declined — and "declined" has to be a row rather than a missing one, because a missing row
/// also means "the call ended before we got here". The dashboard needs to tell a caller who said
/// no from a caller who hung up, and the difference is where a survey is losing people.
///
/// The open answer is the transcript of what they said, verbatim. That is why this module turns
/// caller transcription on when the others leave it off: without the words there is nothing to
/// record.</summary>
public sealed class FeedbackAnswer : BaseEntity
{
    public int FeedbackCallId { get; private set; }
    public int SurveyQuestionId { get; private set; }

    /// <summary>Set for a Choice question. Null for an open answer or a decline.</summary>
    public int? SurveyQuestionOptionId { get; private set; }

    /// <summary>The caller's own words. Set for an Open question, and also for "Digər" — an
    /// option chosen because the list was wrong is worth nothing without what they actually
    /// said. Null otherwise.</summary>
    public string? Text { get; private set; }

    /// <summary>They were asked and would not say. Distinct from never having been asked.</summary>
    public bool Declined { get; private set; }

    public Instant AnsweredAt { get; private set; }

    private FeedbackAnswer()
    {
    }

    public static FeedbackAnswer Chose(int callId, int questionId, int optionId, Instant now)
    {
        var answer = New(callId, questionId, now);
        answer.SurveyQuestionOptionId = optionId;
        return answer;
    }

    /// <summary>Picked "Digər", and said what it was.
    ///
    /// Both halves are kept: the option so the count is right, the words so the count means
    /// something. Recording only the option would turn every unanticipated answer into an
    /// undifferentiated pile.</summary>
    public static FeedbackAnswer ChoseOther(int callId, int questionId, int optionId, string text, Instant now)
    {
        var answer = Chose(callId, questionId, optionId, now);
        answer.Text = string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        return answer;
    }

    public static FeedbackAnswer Said(int callId, int questionId, string text, Instant now)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException(
                "An open answer with no words is a decline, not an answer — use Refused.", nameof(text));
        }

        var answer = New(callId, questionId, now);
        answer.Text = text.Trim();
        return answer;
    }

    /// <summary>Asked, and would not say.</summary>
    public static FeedbackAnswer Refused(int callId, int questionId, Instant now)
    {
        var answer = New(callId, questionId, now);
        answer.Declined = true;
        return answer;
    }

    private static FeedbackAnswer New(int callId, int questionId, Instant now)
    {
        var answer = new FeedbackAnswer
        {
            FeedbackCallId = callId,
            SurveyQuestionId = questionId,
            AnsweredAt = now,
        };

        answer.InitBase(now);
        return answer;
    }
}
