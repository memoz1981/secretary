using Secretary.Domain.ValueObjects;

namespace Secretary.Application.Pricing;

/// <summary>What one model consumed on a call, and therefore which rate card prices it.
///
/// A call can have several: the chained voice pipeline bills a transcription model, a text model
/// and a speech model separately, and each has its own rates.</summary>
/// <param name="AudioSeconds">Seconds of audio for providers billed by the minute instead of by
/// the token. whisper-1 is one of them, and pretending its minutes are tokens would make its
/// cost fiction.</param>
public sealed record ModelUsage(string Model, TokenUsage Usage, double AudioSeconds = 0);

/// <summary>The rate card, from configuration — see appsettings.json's "ModelPricing" section
/// and backend/README.md, "What a call costs".
///
/// Editing these rates deliberately does NOT change what past calls cost: the price of a call
/// is worked out once and stored on the Call row. These values only ever price calls made from
/// now on, which is what makes them safe to correct when the provider changes its pricing.</summary>
public sealed class ModelPricingOptions
{
    public const string SectionName = "ModelPricing";

    /// <summary>Keyed by the model name exactly as it is sent to the provider (and stored on
    /// Call.AgentModel), e.g. "gpt-realtime-2.1". Lookup is case-insensitive.</summary>
    public Dictionary<string, ModelTokenRates> Models { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>USD per 1,000,000 tokens, split the way providers actually bill. Six numbers and
/// not fewer: on gpt-realtime-2.1 an audio output token costs 160× a cached input one, so a
/// blended "per token" rate would misprice a call by whatever the mix happened to be.</summary>
public sealed class ModelTokenRates
{
    public decimal TextInputPerMillionUsd { get; set; }
    public decimal CachedTextInputPerMillionUsd { get; set; }
    public decimal TextOutputPerMillionUsd { get; set; }
    public decimal AudioInputPerMillionUsd { get; set; }
    public decimal CachedAudioInputPerMillionUsd { get; set; }
    public decimal AudioOutputPerMillionUsd { get; set; }

    /// <summary>For models billed by audio duration rather than tokens — whisper-1 charges
    /// $0.006 a minute and reports no token counts at all. Zero for everything else.</summary>
    public decimal PerAudioMinuteUsd { get; set; }
}
