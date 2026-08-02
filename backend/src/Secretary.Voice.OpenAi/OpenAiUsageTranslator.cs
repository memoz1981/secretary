#pragma warning disable OPENAI002 // OpenAI.Realtime is still an evolving/preview surface of the OpenAI SDK.
using Secretary.Domain.ValueObjects;
using OpenAI.Realtime;

namespace Secretary.Voice.OpenAi;

/// <summary>Turns one OpenAI response's usage into the neutral TokenUsage the tally adds up.
///
/// This is the awkward half of cost tracking and it belongs with the provider that reported the
/// numbers: text and audio tokens are priced up to eightfold apart, so a headline total cannot
/// be turned into money on its own.</summary>
public static class OpenAiUsageTranslator
{
    public static TokenUsage? ToTokenUsage(RealtimeResponseUsage? usage)
    {
        if (usage is null)
        {
            return null;
        }

        var input = usage.InputTokenDetails;
        var cached = input?.CachedTokenDetails;

        // Read from the detail breakdown rather than the headline InputTokenCount. When a
        // provider sends no breakdown at all, the headline count is attributed to text — the
        // cheaper of the two, so an unknown mix understates rather than inflates what we report
        // having spent.
        var inputTextTokens = input?.TextTokenCount;
        var inputAudioTokens = input?.AudioTokenCount ?? 0;
        if (inputTextTokens is null && input?.AudioTokenCount is null)
        {
            inputTextTokens = usage.InputTokenCount;
        }

        // Same reasoning one level down: a flat cached count with no text/audio split gets
        // attributed to text. Cached tokens are subtracted from their modality's full-rate
        // total, and text input is the cheaper of the two — so guessing text discounts the call
        // by less than guessing audio would. Wrong in the safe direction.
        var cachedTextTokens = cached?.TextTokenCount;
        var cachedAudioTokens = cached?.AudioTokenCount ?? 0;
        if (cachedTextTokens is null && cached?.AudioTokenCount is null)
        {
            cachedTextTokens = input?.CachedTokenCount;
        }

        return TokenUsage.FromProviderTotals(
            inputTextTokens: inputTextTokens ?? 0,
            cachedInputTextTokens: cachedTextTokens ?? 0,
            inputAudioTokens: inputAudioTokens,
            cachedInputAudioTokens: cachedAudioTokens,
            outputTextTokens: usage.OutputTokenDetails?.TextTokenCount ?? 0,
            outputAudioTokens: usage.OutputTokenDetails?.AudioTokenCount ?? 0);
    }
}
