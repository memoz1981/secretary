using Secretary.Domain.Abstractions;
using Secretary.Domain.Enums;
using Secretary.Domain.ValueObjects;
using NodaTime;

namespace Secretary.Domain.Entities;

/// <summary>The permanent record of one call to the order line, written once when the call ends.
///
/// Its own table in the module's own schema, not a Module column on app.Calls. Same rule the
/// rest of the modules follow: a module owns its tables, and the two records are not the same
/// shape anyway — an appointment call points at an appointment and a client, an order call
/// points at an order and a customer, and neither foreign key can exist on a shared row.
///
/// ⚠ Order calls made before this table existed are still in app.Calls. They were written there
/// because there was nowhere else, and they are not moved: which of those rows were order calls
/// can only be inferred by matching timestamps against orders, and a guess is not something to
/// found a data migration on.
///
/// There is no classification. Whether an order came out of the call is a foreign key, not a
/// label — RelatedOrderId says it exactly and cannot drift from the truth.</summary>
public sealed class OrderCall : BaseEntity
{
    public int TenantId { get; private set; }

    /// <summary>Who the agent identified, when it managed to. Null when the call ended before
    /// anyone was found — which is itself worth seeing on the page.</summary>
    public int? CustomerId { get; private set; }

    /// <summary>The number as dialled in, kept whether or not it resolved to a customer.</summary>
    public string CallerPhoneNumber { get; private set; }

    /// <summary>The order this call produced, if it produced one.</summary>
    public int? RelatedOrderId { get; private set; }

    public CallOutcome Outcome { get; private set; }
    public int DurationSeconds { get; private set; }

    /// <summary>Answers: replies the agent completed.</summary>
    public int TurnCount { get; private set; }

    /// <summary>Questions: times the caller took the turn.</summary>
    public int CallerTurnCount { get; private set; }
    public int? WaitTimeSeconds { get; private set; }
    public string RecordingUrl { get; private set; }
    public string? Transcript { get; private set; }
    public Instant StartedAt { get; private set; }

    // ---- What this call cost to run ----
    // Written once, at the end of the call, and never recalculated — the counts are what the
    // provider reported and CostUsd is those counts at the rates in force at that moment.
    // Re-deriving on read would mean editing a rate silently rewrote history.
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

    private OrderCall()
    {
        CallerPhoneNumber = string.Empty;
        RecordingUrl = string.Empty;
        AgentModel = string.Empty;
    }

    public static OrderCall Log(
        int tenantId, int? customerId, string callerPhoneNumber, int? relatedOrderId, CallOutcome outcome,
        int durationSeconds, int turnCount, int callerTurnCount, int? waitTimeSeconds, string recordingUrl,
        string? transcript, Instant startedAt, string agentModel, CallPipeline pipeline, TokenUsage tokenUsage,
        decimal costUsd, Instant now)
    {
        if (durationSeconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(durationSeconds));
        }

        if (turnCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(turnCount));
        }

        if (callerTurnCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(callerTurnCount));
        }

        if (costUsd < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(costUsd));
        }

        if (string.IsNullOrWhiteSpace(callerPhoneNumber))
        {
            throw new ArgumentException("Caller phone number is required.", nameof(callerPhoneNumber));
        }

        var call = new OrderCall
        {
            TenantId = tenantId,
            CustomerId = customerId,
            CallerPhoneNumber = callerPhoneNumber,
            RelatedOrderId = relatedOrderId,
            Outcome = outcome,
            DurationSeconds = durationSeconds,
            TurnCount = turnCount,
            CallerTurnCount = callerTurnCount,
            WaitTimeSeconds = waitTimeSeconds,
            RecordingUrl = recordingUrl,
            Transcript = transcript,
            StartedAt = startedAt,
            AgentModel = agentModel ?? string.Empty,
            Pipeline = pipeline,
            InputTextTokens = tokenUsage.InputTextTokens,
            CachedInputTextTokens = tokenUsage.CachedInputTextTokens,
            InputAudioTokens = tokenUsage.InputAudioTokens,
            CachedInputAudioTokens = tokenUsage.CachedInputAudioTokens,
            OutputTextTokens = tokenUsage.OutputTextTokens,
            OutputAudioTokens = tokenUsage.OutputAudioTokens,
            CostUsd = costUsd,
        };

        call.InitBase(now);
        return call;
    }
}
