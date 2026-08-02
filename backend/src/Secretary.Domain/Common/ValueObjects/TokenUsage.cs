namespace Secretary.Domain.ValueObjects;

/// <summary>What one call consumed from the model provider, split along the exact six lines
/// OpenAI prices separately. Any coarser split (a single "total tokens") cannot be turned back
/// into money: audio output costs 16× what a cached input token does, so two calls with an
/// identical total can differ in price by an order of magnitude.
///
/// Cached input is tracked SEPARATELY from, not inside, the uncached counts — the provider
/// reports cached tokens as a subset of the input total, and storing them that way once meant
/// the cheap tokens were also billed at the full rate. <see cref="FromProviderTotals"/> is the
/// one place that subtraction happens.</summary>
public sealed record TokenUsage(
    int InputTextTokens,
    int CachedInputTextTokens,
    int InputAudioTokens,
    int CachedInputAudioTokens,
    int OutputTextTokens,
    int OutputAudioTokens)
{
    public static readonly TokenUsage Zero = new(0, 0, 0, 0, 0, 0);

    /// <summary>Every token the call consumed — for display and for reasoning about the
    /// provider's per-minute token limit, never for pricing.</summary>
    public int TotalTokens =>
        InputTextTokens + CachedInputTextTokens + InputAudioTokens
        + CachedInputAudioTokens + OutputTextTokens + OutputAudioTokens;

    /// <summary>Builds usage from the shape providers actually report: input totals that
    /// INCLUDE their cached portion. The cached part is carved out here so the rest can be
    /// charged at the full rate and the cached part at its own much lower one.</summary>
    public static TokenUsage FromProviderTotals(
        int inputTextTokens, int cachedInputTextTokens,
        int inputAudioTokens, int cachedInputAudioTokens,
        int outputTextTokens, int outputAudioTokens)
        => new(
            InputTextTokens: NonNegative(inputTextTokens - cachedInputTextTokens),
            CachedInputTextTokens: NonNegative(cachedInputTextTokens),
            InputAudioTokens: NonNegative(inputAudioTokens - cachedInputAudioTokens),
            CachedInputAudioTokens: NonNegative(cachedInputAudioTokens),
            OutputTextTokens: NonNegative(outputTextTokens),
            OutputAudioTokens: NonNegative(outputAudioTokens));

    /// <summary>A call is the sum of its turns, so usage accumulates rather than replaces.</summary>
    public static TokenUsage operator +(TokenUsage left, TokenUsage right)
        => new(
            left.InputTextTokens + right.InputTextTokens,
            left.CachedInputTextTokens + right.CachedInputTextTokens,
            left.InputAudioTokens + right.InputAudioTokens,
            left.CachedInputAudioTokens + right.CachedInputAudioTokens,
            left.OutputTextTokens + right.OutputTextTokens,
            left.OutputAudioTokens + right.OutputAudioTokens);

    /// <summary>A provider that reports a cached count larger than the total it belongs to
    /// would otherwise produce negative billable tokens — a call that costs less than nothing.</summary>
    private static int NonNegative(int value) => value < 0 ? 0 : value;
}
