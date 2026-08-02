using Google.GenAI.Types;
using Secretary.Domain.ValueObjects;

namespace Secretary.Voice.Google;

/// <summary>Turns Gemini's usage report into the neutral TokenUsage the tally adds up.
///
/// Gemini reports totals plus a per-modality breakdown (PromptTokensDetails and friends), where
/// OpenAI reports a nested text/audio/cached structure. Same job either way: audio and text are
/// priced several-fold apart, so a headline total cannot be turned into money on its own.</summary>
public static class GeminiUsageTranslator
{
    public static TokenUsage? ToTokenUsage(UsageMetadata? usage)
    {
        if (usage is null)
        {
            return null;
        }

        var promptText = TokensFor(usage.PromptTokensDetails, MediaModality.Text);
        var promptAudio = TokensFor(usage.PromptTokensDetails, MediaModality.Audio);
        var responseText = TokensFor(usage.ResponseTokensDetails, MediaModality.Text);
        var responseAudio = TokensFor(usage.ResponseTokensDetails, MediaModality.Audio);
        var cachedText = TokensFor(usage.CacheTokensDetails, MediaModality.Text);
        var cachedAudio = TokensFor(usage.CacheTokensDetails, MediaModality.Audio);

        // No breakdown at all: attribute the headline count to text, the cheaper modality, so an
        // unknown mix understates rather than inflates what we report having spent. Same choice
        // as the OpenAI translator makes, and wrong in the same safe direction.
        if (promptText == 0 && promptAudio == 0)
        {
            promptText = usage.PromptTokenCount ?? 0;
        }

        if (responseText == 0 && responseAudio == 0)
        {
            responseText = usage.ResponseTokenCount ?? 0;
        }

        if (cachedText == 0 && cachedAudio == 0)
        {
            cachedText = usage.CachedContentTokenCount ?? 0;
        }

        // Thinking tokens are billed at the output text rate and reported outside the modality
        // breakdown, so they would otherwise vanish from the bill entirely.
        responseText += usage.ThoughtsTokenCount ?? 0;

        return TokenUsage.FromProviderTotals(
            inputTextTokens: promptText,
            cachedInputTextTokens: cachedText,
            inputAudioTokens: promptAudio,
            cachedInputAudioTokens: cachedAudio,
            outputTextTokens: responseText,
            outputAudioTokens: responseAudio);
    }

    private static int TokensFor(List<ModalityTokenCount>? details, MediaModality modality)
        => details?
            .Where(d => d.Modality == modality)
            .Sum(d => d.TokenCount ?? 0)
        ?? 0;
}
