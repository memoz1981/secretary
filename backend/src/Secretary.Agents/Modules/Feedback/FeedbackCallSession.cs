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

    /// <summary>How many times a question has been answered with something unmatchable.
    ///
    /// ⚠ Counted here because a survey that cannot get past a question is worse than a survey
    /// missing one answer. On a real call the caller said "dörd", "beş", "üç" — all of them
    /// refused — and the agent asked the same question five times before they gave up. The
    /// matcher was at fault that time, but no matcher will ever be right for everything anybody
    /// says, so the loop needs an end that does not depend on one being right.</summary>
    private readonly Dictionary<int, int> _failedAttempts = [];

    /// <summary>Records a failure and says whether this question has now had enough.</summary>
    public bool TooManyFailuresFor(int questionId)
    {
        _failedAttempts[questionId] = _failedAttempts.GetValueOrDefault(questionId) + 1;
        return _failedAttempts[questionId] >= MaxAttemptsPerQuestion;
    }

    /// <summary>Two goes at understanding an answer. A third reading of the same options is a
    /// caller being argued with.</summary>
    private const int MaxAttemptsPerQuestion = 2;

    /// <summary>The id, or a refusal. A survey tool without one has nothing to record against,
    /// and recording against the wrong call would put one person's answers under another's
    /// name.</summary>
    public int Require()
        => CallId ?? throw new InvalidOperationException(
            "This call was not queued — a feedback call must be created before it is dialled.");
}
