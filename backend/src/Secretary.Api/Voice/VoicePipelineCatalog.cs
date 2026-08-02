using Secretary.Domain.Enums;

namespace Secretary.Api.Voice;

/// <summary>What each dialable pipeline actually is: which architecture and which model.
///
/// One table so the endpoint, the call log and the Call page legend cannot drift apart. Adding
/// an option means one entry here plus its rate card — which holds for another realtime model,
/// but not for Gemini Live: that speaks a different wire protocol and will need its own session
/// implementation behind a shared abstraction. See design/architecture.md §3.</summary>
public static class VoicePipelineCatalog
{
    /// <param name="Enabled">False for an option that is listed but cannot be dialled yet. The
    /// Call page shows it greyed out rather than hiding it, so the roadmap is visible and the
    /// legend does not silently change shape the day it ships.</param>
    /// <param name="ProviderKey">Matches IRealtimeSession.ProviderKey, and names the instruction
    /// file this pipeline reads — Instructions/PhoneAgent.{ProviderKey}.md.</param>
    public sealed record Entry(
        CallPipeline Pipeline,
        string Label,
        string ProviderKey,
        string RealtimeModel,
        bool Enabled = true);

    public static readonly IReadOnlyList<Entry> All =
    [
        new(
            CallPipeline.OpenAiRealtime_2_1,
            "OpenAI · realtime 2.1",
            ProviderKey: "openai",
            RealtimeModel: "gpt-realtime-2.1"),

        // Roughly four times cheaper than OpenAI. Whether Azerbaijani survives it is the whole
        // reason it is dialable rather than assumed — that only a real call answers.
        new(
            CallPipeline.GeminiLive_3_1,
            "Gemini · Live 3.1",
            ProviderKey: "gemini",
            RealtimeModel: "gemini-3.1-flash-live-preview"),
    ];

    public static IEnumerable<Entry> Dialable => All.Where(entry => entry.Enabled);

    public static Entry? Find(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            // No pipeline asked for — the realtime path, which is what /voice/live-call meant
            // before there was a choice.
            return Dialable.First();
        }

        return Dialable.FirstOrDefault(entry =>
            string.Equals(entry.Pipeline.ToString(), name, StringComparison.OrdinalIgnoreCase)
            // Also accepts the bare number, because "V1" is how these get talked about.
            || string.Equals($"V{(int)entry.Pipeline}", name, StringComparison.OrdinalIgnoreCase));
    }
}
