using Secretary.Application.Pricing;
using Secretary.Domain.Enums;
using Secretary.Domain.ValueObjects;
using NodaTime;

namespace Secretary.Application.Dtos;

// ---- Questionnaire editing ----

public sealed record SurveyOptionResponse(int Id, int Position, string Text, decimal? ScorePercent, bool IsOther);

public sealed record SurveyQuestionResponse(
    int Id,
    int Position,
    string Text,
    FeedbackQuestionType QuestionType,
    bool CountsTowardScore,
    int? ScaleMax,
    bool YesIsPositive,
    bool AllowOther,
    IReadOnlyList<SurveyOptionResponse> Options)
{
    /// <summary>Read off the type, exactly as the entity does. It used to be read off "do all the
    /// options carry numbers", which was true by accident whenever somebody filled the boxes in.</summary>
    public bool IsScored => QuestionType is FeedbackQuestionType.YesNo or FeedbackQuestionType.Scale;
}

public sealed record SurveyResponse(
    int Id, string Name, int QuestionCount, int RetryCount, int RetryDelayMinutes);

public sealed record SurveyDetailResponse(int Id, string Name, IReadOnlyList<SurveyQuestionResponse> Questions);

public sealed record SaveSurveyRequest(string Name);

/// <summary>What the question editor sends.
///
/// ⚠ No scores. The owner types labels for a Choice question and nothing else numeric, anywhere:
/// Yes/No and Scale generate their own options and their own percentages. The field this replaces
/// was a free number box per option, and it produced a live questionnaire scoring Bəli=1, Xeyr=2.
///
/// Fields that do not apply to the chosen type are ignored rather than rejected, so switching the
/// type in the form does not have to clear them first.</summary>
public sealed record SaveQuestionRequest(
    string Text,
    FeedbackQuestionType QuestionType,
    bool CountsTowardScore = false,
    int? ScaleMax = null,
    bool YesIsPositive = true,
    bool AllowOther = false,
    IReadOnlyList<string>? Labels = null);

/// <summary>The whole running order in one call. Reordering is a drag on the page and sending
/// one request per moved question would leave the list half-reordered if any of them failed.</summary>
public sealed record ReorderQuestionsRequest(IReadOnlyList<int> QuestionIdsInOrder);

public sealed record FeedbackSettingsResponse(int MaxSurveys, int UsedSurveys);

public sealed record UpdateFeedbackSettingsRequest(int MaxSurveys);

// ---- Calls ----

/// <summary>What the form sends. Name and number only — anything else is something the caller
/// would have to be asked twice.</summary>
public sealed record QueueFeedbackCallRequest(int SurveyId, string PersonName, string PhoneNumber);

/// <summary>One person to be surveyed, with every attempt against them rolled up.
///
/// This is what the follow-up list is made of, and the reason it exists: five rows for one person
/// answered "how many times did we ring" as if it were "how many people did we survey".</summary>
public sealed record SurveyRequestResponse(
    int Id,
    int SurveyId,
    string SurveyName,
    string PersonName,
    string PhoneNumber,
    SurveyRequestOutcome Outcome,
    int AttemptCount,
    Instant CreatedAt,
    Instant? LastAttemptAt,
    Instant? NextAttemptDueAt,
    bool NeedsFollowUp,
    decimal TotalCostUsd);

public sealed record SaveRetryPolicyRequest(int RetryCount, int RetryDelayMinutes);

public sealed record FeedbackCallResponse(
    int Id,
    int SurveyRequestId,
    int SurveyId,
    string SurveyName,
    string PersonName,
    string PhoneNumber,
    int AttemptNumber,
    FeedbackCallStatus Status,
    Instant CreatedAt,
    Instant? CompletedAt,
    int DurationSeconds,
    int TurnCount,
    int CallerTurnCount,
    string AgentModel,
    CallPipeline Pipeline,
    TokenUsage TokenUsage,
    decimal CostUsd,
    int AnsweredCount,
    int QuestionCount)
{
    public bool IsCompleted => Status == FeedbackCallStatus.Completed;

    /// <summary>Null for a call that never started — a rate over nothing is not zero.</summary>
    public decimal? CostPerMinuteUsd => DurationSeconds <= 0 ? null : CostUsd * 60m / DurationSeconds;
}

public sealed record FeedbackAnswerResponse(
    int QuestionId,
    int Position,
    string QuestionText,
    FeedbackQuestionType QuestionType,
    string? OptionText,
    decimal? OptionScorePercent,
    string? Text,
    bool Declined,
    Instant AnsweredAt);

public sealed record FeedbackCallDetailResponse(
    FeedbackCallResponse Call,
    IReadOnlyList<FeedbackAnswerResponse> Answers,
    string? Transcript);

// ---- Dashboard ----

/// <summary>How many people we set out to survey, and how far we got with them.
///
/// Counted in people, never in dials. The version this replaces counted rows in fd.Calls, so four
/// dead connections in nine seconds read as four surveys attempted — the numerator and the
/// denominator both wrong, in opposite directions.</summary>
public sealed record FeedbackCoverage(
    int Requested,
    int Completed,
    int NotReached,
    int Refused,
    int NeedsHuman,
    int Attempts,
    int AverageDurationSeconds,
    decimal TotalCostUsd)
{
    /// <summary>People who picked up and engaged. The honest denominator for anything about how
    /// well the agent does its job.</summary>
    public int Reached => Completed + Refused + NeedsHuman;

    /// <summary>Of the people the agent actually spoke to, how many finished.
    ///
    /// ⚠ Divided by Reached and never by Requested. A wrong phone number is not the agent failing
    /// at a conversation, and putting it in this denominator makes the one number that should say
    /// "is the agent any good" say "is the phone list any good" instead.</summary>
    public decimal? CompletionRate => Reached == 0 ? null : (decimal)Completed / Reached;

    /// <summary>Of everybody we meant to survey, how many we got a full survey from. The number
    /// that qualifies the score: 92% from four people out of two hundred is not 92%.</summary>
    public decimal? ResponseRate => Requested == 0 ? null : (decimal)Completed / Requested;
}

public sealed record OptionBreakdown(int OptionId, string Text, decimal? ScorePercent, int Count);

/// <summary>One panel. <c>AveragePercent</c> is null for the types with no ordering — Choice and
/// Open — because a mean over names is a number about nothing.</summary>
public sealed record QuestionResult(
    int QuestionId,
    int Position,
    string QuestionText,
    FeedbackQuestionType QuestionType,
    bool CountsTowardScore,
    bool IsScored,
    int AnsweredCount,
    int DeclinedCount,
    decimal? AveragePercent,
    IReadOnlyList<OptionBreakdown> Options);

/// <summary>⚠ Open questions are deliberately absent from the charts. There is nothing honest to
/// plot, and a word cloud is not an answer — they are read on the call detail page instead.</summary>
/// <param name="ScorePercent">The mean of every answer to a question ticked "counts toward the
/// score". Null when no question is ticked, or nobody has answered one.</param>
/// <param name="PreviousScorePercent">The same number over the preceding window of equal length.
/// Null for an all-time view, where there is no preceding window to compare with.</param>
/// <param name="ScoreAnswerCount">How many answers the score rests on. Shown beside it always —
/// a score without its denominator is the number people quote.</param>
public sealed record FeedbackDashboardResponse(
    int SurveyId,
    string SurveyName,
    decimal? ScorePercent,
    decimal? PreviousScorePercent,
    int ScoreAnswerCount,
    FeedbackCoverage Coverage,
    IReadOnlyList<QuestionResult> Results);

// ---- The agent's own view of a call in progress ----

/// <summary>What the agent is told about the question it is about to ask.
///
/// <c>ScaleMax</c> and <c>AllowOther</c> are here because they change what the agent says, not
/// only what the service stores: a scale is announced as a range rather than as a list, and a
/// question that allows "Digər" has no such thing as an unmatchable answer.</summary>
public sealed record FeedbackQuestionForAgent(
    int QuestionId,
    int Position,
    string Text,
    FeedbackQuestionType QuestionType,
    int? ScaleMax,
    bool AllowOther,
    IReadOnlyList<SurveyOptionResponse> Options);

public sealed record FeedbackCallSubject(int CallId, string PersonName, string SurveyName, int QuestionCount);

public sealed record LogFeedbackCallRequest(
    int CallId,
    int DurationSeconds,
    int TurnCount,
    int CallerTurnCount,
    string? Transcript,
    CallPipeline Pipeline,
    IReadOnlyList<ModelUsage>? ModelUsages);
