using Secretary.Application.Pricing;
using Secretary.Domain.Enums;
using Secretary.Domain.ValueObjects;
using NodaTime;

namespace Secretary.Application.Dtos;

// ---- Questionnaire editing ----

public sealed record SurveyOptionResponse(int Id, int Position, string Text, int? Value);

public sealed record SurveyQuestionResponse(
    int Id,
    int Position,
    string Text,
    FeedbackQuestionType QuestionType,
    bool IsHeadline,
    IReadOnlyList<SurveyOptionResponse> Options)
{
    /// <summary>Whether these answers can be averaged rather than only counted — every option
    /// carries a number. The dashboard renders a different panel for each case.</summary>
    public bool IsScored => QuestionType == FeedbackQuestionType.Choice
                            && Options.Count > 0
                            && Options.All(o => o.Value is not null);
}

public sealed record SurveyResponse(int Id, string Name, int QuestionCount);

public sealed record SurveyDetailResponse(int Id, string Name, IReadOnlyList<SurveyQuestionResponse> Questions);

public sealed record SaveSurveyRequest(string Name);

public sealed record SaveOptionRequest(string Text, int? Value);

public sealed record SaveQuestionRequest(
    string Text,
    FeedbackQuestionType QuestionType,
    bool IsHeadline,
    IReadOnlyList<SaveOptionRequest> Options);

/// <summary>The whole running order in one call. Reordering is a drag on the page and sending
/// one request per moved question would leave the list half-reordered if any of them failed.</summary>
public sealed record ReorderQuestionsRequest(IReadOnlyList<int> QuestionIdsInOrder);

public sealed record FeedbackSettingsResponse(int MaxSurveys, int UsedSurveys);

public sealed record UpdateFeedbackSettingsRequest(int MaxSurveys);

// ---- Calls ----

/// <summary>What the form sends. Name and number only — anything else is something the caller
/// would have to be asked twice.</summary>
public sealed record QueueFeedbackCallRequest(int SurveyId, string PersonName, string PhoneNumber);

public sealed record FeedbackCallResponse(
    int Id,
    int SurveyId,
    string SurveyName,
    string PersonName,
    string PhoneNumber,
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
    int? OptionValue,
    string? Text,
    bool Declined,
    Instant AnsweredAt);

public sealed record FeedbackCallDetailResponse(
    FeedbackCallResponse Call,
    IReadOnlyList<FeedbackAnswerResponse> Answers,
    string? Transcript);

// ---- Dashboard ----

/// <summary>How the agent itself performed, and it does not depend on what was asked — which is
/// the point. These numbers are comparable across tenants and across questionnaires.</summary>
public sealed record FeedbackAgentStats(
    int CallsQueued,
    int CallsStarted,
    int CallsCompleted,
    int CallsAbandoned,
    int AverageDurationSeconds,
    decimal TotalCostUsd)
{
    /// <summary>Of the calls that got going, how many reached the last question. Null when none
    /// started, because a percentage of nothing reads as failure.</summary>
    public decimal? CompletionRate => CallsStarted == 0 ? null : (decimal)CallsCompleted / CallsStarted;
}

/// <summary>Where people hang up. Question-agnostic, and the most useful thing on the page —
/// a question that loses a third of callers is a question worth rewriting.</summary>
public sealed record QuestionDropOff(int QuestionId, int Position, string QuestionText, int Reached, int Answered);

public sealed record OptionBreakdown(int OptionId, string Text, int? Value, int Count);

/// <summary>One panel. Average is null unless every option carries a number — see IsScored.</summary>
public sealed record QuestionResult(
    int QuestionId,
    int Position,
    string QuestionText,
    bool IsHeadline,
    bool IsScored,
    int AnsweredCount,
    int DeclinedCount,
    decimal? Average,
    IReadOnlyList<OptionBreakdown> Options);

/// <summary>⚠ Open questions are deliberately absent from the charts. There is nothing honest to
/// plot, and a word cloud is not an answer — they are read on the call detail page instead.</summary>
public sealed record FeedbackDashboardResponse(
    int SurveyId,
    string SurveyName,
    FeedbackAgentStats Agent,
    IReadOnlyList<QuestionResult> Results,
    IReadOnlyList<QuestionDropOff> DropOff);

// ---- The agent's own view of a call in progress ----

public sealed record FeedbackQuestionForAgent(
    int QuestionId,
    int Position,
    string Text,
    FeedbackQuestionType QuestionType,
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
