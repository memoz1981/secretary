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

        // Audio comes from the breakdown; everything else is whatever the headline total has
        // left over.
        //
        // ⚠ The breakdown does NOT add up to the total, and the gap is not small. A measured
        // Gemini call reported 2,739 prompt tokens against 2,136 text + 201 audio, and the
        // shortfall grew to 1,125 by the end of the call. Summing only the two modalities we
        // recognise dropped all of it, so every cost this wrote was 5–15% light. Deriving text
        // by subtraction keeps the total whole whatever Gemini reports the rest under, and puts
        // the unknown on the cheaper modality, which is the same safe direction the OpenAI
        // translator errs in.
        var promptAudio = TokensFor(usage.PromptTokensDetails, MediaModality.Audio);
        var responseAudio = TokensFor(usage.ResponseTokensDetails, MediaModality.Audio);
        var cachedAudio = TokensFor(usage.CacheTokensDetails, MediaModality.Audio);

        var promptText = TextFromTotal(
            usage.PromptTokenCount, promptAudio, usage.PromptTokensDetails);

        var responseText = TextFromTotal(
            usage.ResponseTokenCount, responseAudio, usage.ResponseTokensDetails);

        var cachedText = TextFromTotal(
            usage.CachedContentTokenCount, cachedAudio, usage.CacheTokensDetails);

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

    /// <summary>Everything in the total that is not audio. Falls back to the reported text
    /// modality when there is no total to subtract from, which is what a provider that only
    /// fills in the breakdown would give us.</summary>
    private static int TextFromTotal(int? total, int audio, List<ModalityTokenCount>? details)
        => total is > 0 ? Math.Max(0, total.Value - audio) : TokensFor(details, MediaModality.Text);

    private static int TokensFor(List<ModalityTokenCount>? details, MediaModality modality)
        => details?
            .Where(d => d.Modality == modality)
            .Sum(d => d.TokenCount ?? 0)
        ?? 0;
}
