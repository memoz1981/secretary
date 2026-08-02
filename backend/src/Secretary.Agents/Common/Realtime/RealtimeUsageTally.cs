#pragma warning disable OPENAI002 // OpenAI.Realtime is still an evolving/preview surface of the OpenAI SDK.
using Secretary.Domain.ValueObjects;
using OpenAI.Realtime;

namespace Secretary.Agents.Realtime;

/// <summary>Adds up what a call consumed, one response at a time.
///
/// OpenAI reports usage per response, and a response is billed the ENTIRE conversation so far
/// plus what it generates — so these per-response figures grow through the call and the call's
/// true cost is their sum, not the last one. That is also why a long call costs far more than
/// twice a short one, and why the number this produces is worth showing next to the transcript.
///
/// Separate from the orchestrator because the orchestrator needs a live WebSocket to do
/// anything at all, and arithmetic this easy to get quietly wrong deserves tests.</summary>
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
    public void Add(RealtimeResponseUsage? usage)
    {
        if (usage is null)
        {
            return;
        }

        var input = usage.InputTokenDetails;
        var cached = input?.CachedTokenDetails;

        // Text and audio are read from the detail breakdown rather than the headline
        // InputTokenCount: the two are priced eight-fold apart, so a total alone can't be
        // turned into money. When a provider sends no breakdown at all, the headline count is
        // attributed to text — the cheaper of the two, so an unknown mix understates rather
        // than inflates what we report having spent.
        var inputTextTokens = input?.TextTokenCount;
        var inputAudioTokens = input?.AudioTokenCount ?? 0;
        if (inputTextTokens is null && input?.AudioTokenCount is null)
        {
            inputTextTokens = usage.InputTokenCount;
        }

        // Same reasoning one level down: a flat cached count with no text/audio split gets
        // attributed to text. Cached tokens are subtracted from their modality's full-rate
        // total, and text input is the cheaper of the two — so guessing text discounts the
        // call by less than guessing audio would. Wrong in the safe direction.
        var cachedTextTokens = cached?.TextTokenCount;
        var cachedAudioTokens = cached?.AudioTokenCount ?? 0;
        if (cachedTextTokens is null && cached?.AudioTokenCount is null)
        {
            cachedTextTokens = input?.CachedTokenCount;
        }

        var contribution = TokenUsage.FromProviderTotals(
            inputTextTokens: inputTextTokens ?? 0,
            cachedInputTextTokens: cachedTextTokens ?? 0,
            inputAudioTokens: inputAudioTokens,
            cachedInputAudioTokens: cachedAudioTokens,
            outputTextTokens: usage.OutputTokenDetails?.TextTokenCount ?? 0,
            outputAudioTokens: usage.OutputTokenDetails?.AudioTokenCount ?? 0);

        lock (_gate)
        {
            _total += contribution;
        }
    }
}
