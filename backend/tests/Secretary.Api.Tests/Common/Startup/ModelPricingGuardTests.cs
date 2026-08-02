using Secretary.Api.Startup;
using Microsoft.Extensions.Configuration;
using Shouldly;
using Xunit;

namespace Secretary.Api.Tests.Startup;

/// <summary>This guard is what stops the cost tracking in backend/README.md from being quietly
/// lost the next time someone swaps the realtime model. Worth its own tests precisely because
/// nothing else fails when it is wrong — calls just start recording as free.</summary>
public sealed class ModelPricingGuardTests
{
    private static IConfiguration Configuration(params (string Key, string Value)[] entries)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(entries.Select(e => new KeyValuePair<string, string?>(e.Key, e.Value)))
            .Build();

    [Fact]
    public void A_model_with_a_rate_card_starts_normally()
    {
        var configuration = Configuration(
            ("OpenAiRealtime:Model", "gpt-realtime-2.1"),
            ("ModelPricing:Models:gpt-realtime-2.1:AudioOutputPerMillionUsd", "64.00"));

        Should.NotThrow(() => ModelPricingGuard.EnsureConfiguredModelIsPriced(configuration));
    }

    [Fact]
    public void Swapping_in_a_model_without_adding_its_rates_refuses_to_start()
    {
        // The scenario this exists for: someone changes the model, everything still works, and
        // every call from then on is recorded as costing nothing.
        var configuration = Configuration(
            ("OpenAiRealtime:Model", "gpt-realtime-3"),
            ("ModelPricing:Models:gpt-realtime-2.1:AudioOutputPerMillionUsd", "64.00"));

        var exception = Should.Throw<InvalidOperationException>(
            () => ModelPricingGuard.EnsureConfiguredModelIsPriced(configuration));

        exception.Message.ShouldContain("gpt-realtime-3");
        exception.Message.ShouldContain("ModelPricing:Models:gpt-realtime-3");
    }

    [Fact]
    public void A_missing_pricing_section_entirely_refuses_to_start()
    {
        var configuration = Configuration(("OpenAiRealtime:Model", "gpt-realtime-2.1"));

        Should.Throw<InvalidOperationException>(
            () => ModelPricingGuard.EnsureConfiguredModelIsPriced(configuration));
    }

    [Fact]
    public void No_realtime_model_configured_is_not_an_error()
    {
        // A deployment with no voice agent wired up bills nothing, so there is nothing to price.
        Should.NotThrow(() => ModelPricingGuard.EnsureConfiguredModelIsPriced(Configuration()));
    }

    [Fact]
    public void Model_names_are_matched_case_insensitively()
    {
        // Configuration keys are case-insensitive and so is the pricebook's own lookup; the
        // guard must agree with it, or it would reject a model that in fact prices correctly.
        var configuration = Configuration(
            ("OpenAiRealtime:Model", "GPT-Realtime-2.1"),
            ("ModelPricing:Models:gpt-realtime-2.1:AudioOutputPerMillionUsd", "64.00"));

        Should.NotThrow(() => ModelPricingGuard.EnsureConfiguredModelIsPriced(configuration));
    }
}
