using Secretary.Application.Pricing;

namespace Secretary.Api.Startup;

/// <summary>Refuses to start the API when the realtime model in use has no rate card.
///
/// This is the one safeguard behind the cost-tracking requirement in backend/README.md. The
/// failure it prevents is quiet and permanent: an unpriced model records every call it serves
/// as costing $0.00, and because a call's price is written once and never recalculated, no
/// later fix recovers the real figures. A startup crash with a clear message is the cheapest
/// possible version of that discovery.</summary>
public static class ModelPricingGuard
{
    public static void EnsureConfiguredModelIsPriced(IConfiguration configuration)
    {
        var pricing = configuration.GetSection(ModelPricingOptions.SectionName).Get<ModelPricingOptions>();

        // Realtime speech-to-speech bills exactly one model per call. The chained pipelines
        // that billed three are gone; when Gemini Live lands it bills one of its own, checked
        // the same way.
        Require(pricing, configuration.GetSection("OpenAiRealtime")["Model"], "realtime model");
    }

    private static void Require(ModelPricingOptions? pricing, string? model, string description)
    {
        if (string.IsNullOrWhiteSpace(model))
        {
            // Not configured at all — nothing will bill anything under this name.
            return;
        }

        if (pricing is not null && pricing.Models.ContainsKey(model))
        {
            return;
        }

        throw new InvalidOperationException(
            $"No token rates configured for {description} '{model}'. Add a "
            + $"\"{ModelPricingOptions.SectionName}:Models:{model}\" entry (see appsettings.json) with the "
            + "provider's current per-million-token prices, otherwise every call it serves would be "
            + "recorded as costing nothing. See backend/README.md, \"What a call costs\".");
    }
}
