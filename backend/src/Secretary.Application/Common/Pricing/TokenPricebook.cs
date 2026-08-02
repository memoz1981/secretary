using Secretary.Domain.ValueObjects;
using Microsoft.Extensions.Options;

namespace Secretary.Application.Pricing;

/// <summary>Turns tokens into money. Lives in Application rather than in the Agents project on
/// purpose: every path that logs a call — the live voice agent today, telephony or a different
/// provider tomorrow — prices it through here, so adding a new one can't quietly ship without
/// cost tracking.</summary>
public sealed class TokenPricebook
{
    private readonly ModelPricingOptions _options;

    public TokenPricebook(IOptions<ModelPricingOptions> options)
    {
        _options = options.Value;
    }

    /// <summary>Prices a call served by more than one model.
    ///
    /// The realtime path uses a single model, but the chained path spends in three places —
    /// recognition, the text model, and synthesis — at rates that differ by more than an order
    /// of magnitude. Summing their tokens first and pricing once would be meaningless, so each
    /// component is priced against its own rate card and only the money is added up.</summary>
    public decimal CostUsd(IEnumerable<ModelUsage> usages)
        => usages.Sum(usage => CostUsd(usage.Model, usage.Usage) + AudioCostUsd(usage.Model, usage.AudioSeconds));

    /// <summary>The by-the-minute half of the bill. Only whisper-style models charge this way,
    /// and they report no tokens at all — so a call served by one would otherwise be recorded as
    /// free.</summary>
    private decimal AudioCostUsd(string model, double audioSeconds)
    {
        if (audioSeconds <= 0
            || string.IsNullOrWhiteSpace(model)
            || !_options.Models.TryGetValue(model, out var rates)
            || rates.PerAudioMinuteUsd <= 0)
        {
            return 0m;
        }

        return (decimal)(audioSeconds / 60d) * rates.PerAudioMinuteUsd;
    }

    public bool HasRatesFor(string model)
        => !string.IsNullOrWhiteSpace(model) && _options.Models.ContainsKey(model);

    /// <summary>What <paramref name="usage"/> cost on <paramref name="model"/>, in USD.
    ///
    /// An unknown model yields 0 rather than an exception: this runs while a finished call is
    /// being written down, and throwing there would lose the call record entirely to save a
    /// number. The guard against a silently-unpriced model is at startup instead — see
    /// <see cref="HasRatesFor"/> and the check in the API's DI wiring, which refuses to boot
    /// when the configured realtime model has no rate card.</summary>
    public decimal CostUsd(string model, TokenUsage usage)
    {
        if (string.IsNullOrWhiteSpace(model) || !_options.Models.TryGetValue(model, out var rates))
        {
            return 0m;
        }

        // Rounded to the cent only for display; stored at full precision because a single call
        // costs a fraction of a cent in some categories and rounding each line item would drift
        // the monthly total noticeably.
        return Price(usage.InputTextTokens, rates.TextInputPerMillionUsd)
            + Price(usage.CachedInputTextTokens, rates.CachedTextInputPerMillionUsd)
            + Price(usage.InputAudioTokens, rates.AudioInputPerMillionUsd)
            + Price(usage.CachedInputAudioTokens, rates.CachedAudioInputPerMillionUsd)
            + Price(usage.OutputTextTokens, rates.TextOutputPerMillionUsd)
            + Price(usage.OutputAudioTokens, rates.AudioOutputPerMillionUsd);
    }

    private static decimal Price(int tokens, decimal ratePerMillionUsd)
        => tokens <= 0 ? 0m : tokens * ratePerMillionUsd / 1_000_000m;
}
