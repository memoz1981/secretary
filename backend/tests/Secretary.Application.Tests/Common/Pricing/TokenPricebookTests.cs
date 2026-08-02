using Secretary.Application.Pricing;
using Secretary.Domain.ValueObjects;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Secretary.Application.Tests.Pricing;

/// <summary>The rates below are OpenAI's published gpt-realtime-2.1 card (verified 2026-07-27),
/// so these assertions can be checked against the real thing rather than against numbers chosen
/// to make the test pass.</summary>
public sealed class TokenPricebookTests
{
    private const string Model = "gpt-realtime-2.1";

    private static TokenPricebook Pricebook(params (string Model, ModelTokenRates Rates)[] entries)
    {
        var options = new ModelPricingOptions();
        foreach (var (model, rates) in entries)
        {
            options.Models[model] = rates;
        }

        return new TokenPricebook(Options.Create(options));
    }

    private static ModelTokenRates RealtimeRates() => new()
    {
        TextInputPerMillionUsd = 4.00m,
        CachedTextInputPerMillionUsd = 0.40m,
        TextOutputPerMillionUsd = 24.00m,
        AudioInputPerMillionUsd = 32.00m,
        CachedAudioInputPerMillionUsd = 0.40m,
        AudioOutputPerMillionUsd = 64.00m,
    };

    [Fact]
    public void Each_kind_of_token_is_charged_at_its_own_rate()
    {
        var pricebook = Pricebook((Model, RealtimeRates()));

        // One million of each, so the total is simply the six rates added up.
        var million = new TokenUsage(1_000_000, 1_000_000, 1_000_000, 1_000_000, 1_000_000, 1_000_000);

        pricebook.CostUsd(Model, million).ShouldBe(4.00m + 0.40m + 32.00m + 0.40m + 24.00m + 64.00m);
    }

    [Fact]
    public void Audio_output_dominates_a_voice_call()
    {
        var pricebook = Pricebook((Model, RealtimeRates()));

        // The same 100k tokens, spoken by the agent versus cached from the conversation: 160×
        // apart. This is why the six-way split exists instead of one blended rate.
        var spoken = pricebook.CostUsd(Model, new TokenUsage(0, 0, 0, 0, 0, 100_000));
        var cached = pricebook.CostUsd(Model, new TokenUsage(0, 0, 0, 100_000, 0, 0));

        spoken.ShouldBe(6.40m);
        cached.ShouldBe(0.04m);
    }

    [Fact]
    public void A_realistic_call_prices_out_to_cents()
    {
        var pricebook = Pricebook((Model, RealtimeRates()));

        // Roughly one measured call: mostly cached audio input, a few thousand tokens spoken.
        var usage = new TokenUsage(
            InputTextTokens: 3_000, CachedInputTextTokens: 25_000,
            InputAudioTokens: 4_000, CachedInputAudioTokens: 60_000,
            OutputTextTokens: 200, OutputAudioTokens: 3_500);

        // 0.012 + 0.01 + 0.128 + 0.024 + 0.0048 + 0.224
        pricebook.CostUsd(Model, usage).ShouldBe(0.4028m);
    }

    [Fact]
    public void Model_names_match_regardless_of_case()
    {
        var pricebook = Pricebook((Model, RealtimeRates()));

        pricebook.CostUsd("GPT-Realtime-2.1", new TokenUsage(0, 0, 0, 0, 0, 1_000_000)).ShouldBe(64.00m);
        pricebook.HasRatesFor("GPT-REALTIME-2.1").ShouldBeTrue();
    }

    [Fact]
    public void An_unpriced_model_costs_zero_rather_than_throwing()
    {
        // Deliberate: this runs while a finished call is being written down. The guard against
        // an unpriced model reaching production is the startup check that uses HasRatesFor.
        var pricebook = Pricebook((Model, RealtimeRates()));

        pricebook.CostUsd("gpt-realtime-9", new TokenUsage(0, 0, 0, 0, 0, 1_000_000)).ShouldBe(0m);
        pricebook.HasRatesFor("gpt-realtime-9").ShouldBeFalse();
    }

    [Fact]
    public void A_call_with_no_model_at_all_costs_nothing()
    {
        // Staff handled it; no model was involved, so zero is the honest answer, not a gap.
        var pricebook = Pricebook((Model, RealtimeRates()));

        pricebook.CostUsd("", TokenUsage.Zero).ShouldBe(0m);
        pricebook.HasRatesFor("").ShouldBeFalse();
    }

    [Fact]
    public void Fractions_of_a_cent_are_kept_rather_than_rounded_away()
    {
        var pricebook = Pricebook((Model, RealtimeRates()));

        // 100 cached audio tokens at $0.40/M is $0.00004. Rounded to the cent it vanishes, and
        // a month of calls would drift away from the provider's invoice one turn at a time.
        pricebook.CostUsd(Model, new TokenUsage(0, 0, 0, 100, 0, 0)).ShouldBe(0.00004m);
    }

    [Fact]
    public void Different_models_are_priced_from_their_own_cards()
    {
        var pricebook = Pricebook(
            (Model, RealtimeRates()),
            ("gpt-realtime-2.1-mini", new ModelTokenRates
            {
                TextInputPerMillionUsd = 0.60m,
                CachedTextInputPerMillionUsd = 0.06m,
                TextOutputPerMillionUsd = 2.40m,
                AudioInputPerMillionUsd = 10.00m,
                CachedAudioInputPerMillionUsd = 0.30m,
                AudioOutputPerMillionUsd = 20.00m,
            }));

        var usage = new TokenUsage(0, 0, 0, 0, 0, 1_000_000);

        pricebook.CostUsd(Model, usage).ShouldBe(64.00m);
        pricebook.CostUsd("gpt-realtime-2.1-mini", usage).ShouldBe(20.00m);
    }
}
