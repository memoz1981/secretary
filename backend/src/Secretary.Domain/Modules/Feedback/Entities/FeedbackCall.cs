using Secretary.Domain.Abstractions;
using Secretary.Domain.Enums;
using Secretary.Domain.ValueObjects;
using NodaTime;

namespace Secretary.Domain.Entities;

/// <summary>One survey call: who it is about, which questionnaire, and what it cost.
///
/// ⚠ Created BEFORE the call, not after it. Every other module logs a call when it ends, because
/// nobody knows who was on the line until the agent works it out. Here the row comes first — the
/// form (later the scheduler) says who is being rung and about what, and the agent is handed the
/// id. That ordering is the whole reason this module can become outbound without being rewritten:
/// outbound's defining feature is not the dialling, it is knowing the subject before the call
/// starts, and this already does.
///
/// So the person is named here rather than joined to an ord.Customer or an app.Client. They may
/// well be one, but the survey does not care and a link would make a demo call depend on a
/// customer record existing.</summary>
public sealed class FeedbackCall : BaseEntity
{
    public int TenantId { get; private set; }
    public int SurveyId { get; private set; }

    public string PersonName { get; private set; }
    public string PhoneNumber { get; private set; }

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

    private FeedbackCall()
    {
        PersonName = string.Empty;
        PhoneNumber = string.Empty;
        AgentModel = string.Empty;
    }

    /// <summary>Queued, ready to be dialled. The only thing known at this point is who and which
    /// questionnaire — everything else is filled in when the call happens.</summary>
    public static FeedbackCall Queue(
        int tenantId, int surveyId, string personName, string phoneNumber, Instant now)
    {
        if (string.IsNullOrWhiteSpace(personName))
        {
            throw new ArgumentException("A survey call needs somebody to be about.", nameof(personName));
        }

        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            throw new ArgumentException("A survey call needs a number.", nameof(phoneNumber));
        }

        var call = new FeedbackCall
        {
            TenantId = tenantId,
            SurveyId = surveyId,
            PersonName = personName.Trim(),
            PhoneNumber = phoneNumber.Trim(),
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

    /// <summary>Written when the call ends, whatever happened. Completed is not overwritten — a
    /// survey answered to the last question and then hung up on is finished, not abandoned.</summary>
    public void Finish(
        int durationSeconds, int turnCount, int callerTurnCount, string? transcript,
        string agentModel, CallPipeline pipeline, TokenUsage usage, decimal costUsd, Instant now)
    {
        if (durationSeconds < 0 || turnCount < 0 || callerTurnCount < 0 || costUsd < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(durationSeconds), "A call cannot have negative figures.");
        }

        if (CallStatus != FeedbackCallStatus.Completed)
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
