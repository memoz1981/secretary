namespace Secretary.Agents.Feedback;

/// <summary>Which queued call this conversation is, for the length of one call.
///
/// Set before the agent starts, from the id the form created — not discovered by the agent the
/// way the order line discovers a customer. That is the whole shape of this module: the subject
/// is known before anybody speaks, which is what outbound means, and it is why the same tools
/// will work unchanged the day a scheduler dials instead of a person clicking.
///
/// Scoped to the call, like every other per-call state here.</summary>
public sealed class FeedbackCallSession
{
    public int? CallId { get; private set; }

    public void For(int callId) => CallId = callId;

    /// <summary>The id, or a refusal. A survey tool without one has nothing to record against,
    /// and recording against the wrong call would put one person's answers under another's
    /// name.</summary>
    public int Require()
        => CallId ?? throw new InvalidOperationException(
            "This call was not queued — a feedback call must be created before it is dialled.");
}
