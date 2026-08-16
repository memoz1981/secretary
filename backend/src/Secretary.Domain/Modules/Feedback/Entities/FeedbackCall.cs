using Secretary.Domain.Abstractions;
using Secretary.Domain.Enums;
using Secretary.Domain.ValueObjects;
using NodaTime;

namespace Secretary.Domain.Entities;

/// <summary>One dial, and what it cost. Who is being rung lives on the SurveyRequest above it.
///
/// ⚠ This used to be both, and the conflation showed up as soon as somebody pressed the button
/// twice: four dead connections in nine seconds became four "calls" beside one real survey, and
/// the owner counted five people surveyed where there had been one.
///
/// The row is still created BEFORE the call rather than logged after it, which is what the other
/// modules do. They have to log after, because nobody knows who was on the line until the agent
/// works it out; here the subject is known before anybody speaks. That ordering is the whole
/// reason this module becomes outbound without being rewritten.</summary>
public sealed class FeedbackCall : BaseEntity
{
    public int TenantId { get; private set; }

    /// <summary>The person this dial was for. Everything about who they are is over there — a
    /// copy here would be a second answer to the same question, free to drift.</summary>
    public int SurveyRequestId { get; private set; }

    public FeedbackCallStatus CallStatus { get; private set; }
    public Instant CreatedAtUtc { get; private set; }
    public Instant? StartedAt { get; private set; }

    /// <summary>Set only when the last question was put to them. Anything else is abandoned, and
    /// the difference is half of what the dashboard reports.</summary>
    public Instant? CompletedAt { get; private set; }

    public int DurationSeconds { get; private set; }
    public int TurnCount { get; private set; }
    public int CallerTurnCount { get; private set; }
    public string? Transcript { get; private set; }

    // ---- What this call cost to run. Same columns and same rule as ord.Calls: written once,
    // priced at the rates in force then, never recalculated.
    public string AgentModel { get; private set; }
    public CallPipeline Pipeline { get; private set; }

    public int InputTextTokens { get; private set; }
    public int CachedInputTextTokens { get; private set; }
    public int InputAudioTokens { get; private set; }
    public int CachedInputAudioTokens { get; private set; }
    public int OutputTextTokens { get; private set; }
    public int OutputAudioTokens { get; private set; }

    public decimal CostUsd { get; private set; }

    public TokenUsage TokenUsage => new(
        InputTextTokens, CachedInputTextTokens, InputAudioTokens,
        CachedInputAudioTokens, OutputTextTokens, OutputAudioTokens);

    /// <summary>What this dial means for the person it was for.
    ///
    /// ⚠ Read from the turn counts, not from a flag the agent sets, because the case that matters
    /// is the one where the agent never got to set anything. Four rows on 15 August had zero turns
    /// and zero cost: the line opened and died in two seconds. Nobody was reached and nobody
    /// decided anything, so those are the only ones worth dialling again.
    ///
    /// Anything with turns on it was a conversation that did not finish, and that is Refused
    /// whether they said no at the start or hung up at question three. There is no partial
    /// outcome: half a survey is not half a result, and its answers are not reported.</summary>
    public SurveyRequestOutcome Outcome => CallStatus switch
    {
        FeedbackCallStatus.Completed => SurveyRequestOutcome.Complete,
        FeedbackCallStatus.NeedsHuman => SurveyRequestOutcome.NeedsHuman,
        FeedbackCallStatus.Created => SurveyRequestOutcome.NotReached,
        _ => CallerTurnCount == 0 ? SurveyRequestOutcome.NotReached : SurveyRequestOutcome.Refused,
    };

    private FeedbackCall() => AgentModel = string.Empty;

    /// <summary>One dial against a request, queued and not yet made.</summary>
    public static FeedbackCall Attempt(int tenantId, int surveyRequestId, Instant now)
    {
        var call = new FeedbackCall
        {
            TenantId = tenantId,
            SurveyRequestId = surveyRequestId,
            CallStatus = FeedbackCallStatus.Created,
            CreatedAtUtc = now,
        };

        call.InitBase(now);
        return call;
    }

    public void Begin(Instant now)
    {
        CallStatus = FeedbackCallStatus.InProgress;
        StartedAt = now;
        Touch(now);
    }

    /// <summary>Every question was put to them. Declining to answer one still counts as put.</summary>
    public void Complete(Instant now)
    {
        CallStatus = FeedbackCallStatus.Completed;
        CompletedAt = now;
        Touch(now);
    }

    /// <summary>The survey could not go on: an answer was put twice and understood neither time.
    ///
    /// Distinct from abandoned, and the distinction is what happens next. Abandoned is retryable —
    /// a line dropped, a bad moment. This is not: the same agent ringing again would fail the same
    /// way, so it goes to a person.</summary>
    public void HandOverToHuman(Instant now)
    {
        if (CallStatus != FeedbackCallStatus.Completed)
        {
            CallStatus = FeedbackCallStatus.NeedsHuman;
        }

        Touch(now);
    }

    /// <summary>Written when the call ends, whatever happened. Completed is not overwritten — a
    /// survey answered to the last question and then hung up on is finished, not abandoned. Nor is
    /// NeedsHuman, or the hand-over would be lost the moment the line closed.</summary>
    public void Finish(
        int durationSeconds, int turnCount, int callerTurnCount, string? transcript,
        string agentModel, CallPipeline pipeline, TokenUsage usage, decimal costUsd, Instant now)
    {
        if (durationSeconds < 0 || turnCount < 0 || callerTurnCount < 0 || costUsd < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(durationSeconds), "A call cannot have negative figures.");
        }

        if (CallStatus is not (FeedbackCallStatus.Completed or FeedbackCallStatus.NeedsHuman))
        {
            CallStatus = FeedbackCallStatus.Abandoned;
        }

        DurationSeconds = durationSeconds;
        TurnCount = turnCount;
        CallerTurnCount = callerTurnCount;
        Transcript = transcript;
        AgentModel = agentModel ?? string.Empty;
        Pipeline = pipeline;
        InputTextTokens = usage.InputTextTokens;
        CachedInputTextTokens = usage.CachedInputTextTokens;
        InputAudioTokens = usage.InputAudioTokens;
        CachedInputAudioTokens = usage.CachedInputAudioTokens;
        OutputTextTokens = usage.OutputTextTokens;
        OutputAudioTokens = usage.OutputAudioTokens;
        CostUsd = costUsd;
        Touch(now);
    }
}
