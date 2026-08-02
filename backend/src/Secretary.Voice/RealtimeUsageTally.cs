using Secretary.Domain.ValueObjects;

namespace Secretary.Voice;

/// <summary>Adds up what a call consumed, one response at a time.
///
/// Providers report usage per response, and a response is billed the ENTIRE conversation so far
/// plus what it generates — so these per-response figures grow through the call and the call's
/// true cost is their sum, not the last one. That is also why a long call costs far more than
/// twice a short one, and why the number this produces is worth showing next to the transcript.
///
/// Takes an already-neutral TokenUsage: the awkward part — deciding how a provider's token
/// breakdown maps onto text/audio/cached, which are priced up to eightfold apart — belongs with
/// the provider that reported it. Separate from the orchestrator because the orchestrator needs
/// a live WebSocket to do anything at all, and arithmetic this easy to get quietly wrong
/// deserves tests.</summary>
public sealed class RealtimeUsageTally
{
    private readonly object _gate = new();
    private TokenUsage _total = TokenUsage.Zero;

    public TokenUsage Total
    {
        get
        {
            lock (_gate)
            {
                return _total;
            }
        }
    }

    /// <summary>Folds one response's usage in. A response that reports none — a rate-limit
    /// rejection, a cancelled barge-in — contributes nothing rather than being skipped by the
    /// caller, so the call site doesn't need its own null dance.</summary>
    public void Add(TokenUsage? usage)
    {
        if (usage is null)
        {
            return;
        }

        lock (_gate)
        {
            _total += usage;
        }
    }
}
