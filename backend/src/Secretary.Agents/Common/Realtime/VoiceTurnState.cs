namespace Secretary.Agents.Realtime;

/// <summary>The rule the whole live-voice path hangs on: OpenAI accepts a new response only
/// when no response is active, and the model must not be asked to speak until every tool the
/// last response requested has reported back. Two racers can be the one that satisfies those
/// conditions — the response finishing, and the last tool finishing — and exactly one of them
/// may act on it. Kept as its own type so that race is unit-testable without a WebSocket,
/// a live call, or an OpenAI key.</summary>
public sealed class VoiceTurnState
{
    private readonly object _gate = new();
    private int _pendingToolCalls;
    private bool _responseActive;
    private bool _followUpNeeded;
    private bool _callerSpeaking;
    private int _generation;

    /// <summary>Incremented every time OpenAI opens a response. A deferred action (see
    /// <see cref="CanRetrySilentResponse"/>) captures this and can then tell whether the world
    /// moved on while it was waiting.</summary>
    public int Generation
    {
        get { lock (_gate) { return _generation; } }
    }

    public bool CallerSpeaking
    {
        get { lock (_gate) { return _callerSpeaking; } }
    }

    public int PendingToolCalls
    {
        get { lock (_gate) { return _pendingToolCalls; } }
    }

    /// <summary>Returns the generation number of the response that just opened.</summary>
    public int BeginResponse()
    {
        lock (_gate)
        {
            _responseActive = true;
            return ++_generation;
        }
    }

    public void EndResponse()
    {
        lock (_gate)
        {
            _responseActive = false;
        }
    }

    /// <summary>Returns true when the caller's speech actually cut into a live response — only
    /// then is there anything to cancel server-side.</summary>
    public bool BeginCallerSpeech()
    {
        lock (_gate)
        {
            _callerSpeaking = true;
            if (!_responseActive)
            {
                return false;
            }

            _responseActive = false;
            return true;
        }
    }

    public void EndCallerSpeech()
    {
        lock (_gate)
        {
            _callerSpeaking = false;
        }
    }

    public void BeginToolCall()
    {
        lock (_gate)
        {
            _pendingToolCalls++;
        }
    }

    /// <summary>Returns how many tool calls are still outstanding.</summary>
    public int EndToolCall()
    {
        lock (_gate)
        {
            _followUpNeeded = true;
            return --_pendingToolCalls;
        }
    }

    /// <summary>Atomically claims the right to ask the model to speak, so the response and the
    /// last tool completion can never both do it.</summary>
    public bool TryClaimFollowUp(out TurnSnapshot snapshot)
    {
        lock (_gate)
        {
            snapshot = new TurnSnapshot(_responseActive, _pendingToolCalls, _followUpNeeded);
            if (_responseActive || _pendingToolCalls > 0 || !_followUpNeeded)
            {
                return false;
            }

            _followUpNeeded = false;
            return true;
        }
    }

    /// <summary>Whether re-asking for a reply that never materialised is still the right thing
    /// to do, a moment after deciding it was. Pointless — and actively harmful, since it burns
    /// a response the caller then has to wait through — if OpenAI has already opened another
    /// response, a tool is running, or the caller is mid-sentence.</summary>
    public bool CanRetrySilentResponse(int generation)
    {
        lock (_gate)
        {
            return _generation == generation && !_responseActive && !_callerSpeaking && _pendingToolCalls == 0;
        }
    }
}

/// <summary>Point-in-time copy of the turn state, for logging a decision without holding the
/// lock while the logger runs.</summary>
public readonly record struct TurnSnapshot(bool ResponseActive, int PendingToolCalls, bool FollowUpNeeded);
