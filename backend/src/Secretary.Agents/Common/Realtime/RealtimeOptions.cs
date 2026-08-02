namespace Secretary.Agents.Realtime;

public sealed class RealtimeOptions
{
    public const string SectionName = "OpenAiRealtime";

    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Verify this is still current before deploying — realtime model names have
    /// changed repeatedly. gpt-realtime-2.1 was current (GPT-5-class reasoning, needed for
    /// the tool-calling/booking logic in a live call) as of when this was built.</summary>
    public string Model { get; set; } = "gpt-realtime-2.1";

    /// <summary>PCM16 — the format the browser's Call page captures and plays,
    /// no telephony codec involved since there's no phone line here (see backend/README.md —
    /// this replaces the originally-discussed Twilio Media Streams direction with a
    /// computer-audio-device call to the web app instead).</summary>
    public string AudioFormat { get; set; } = "pcm16";

    /// <summary>OpenAI's recommended sample rate for Realtime API PCM16 audio.</summary>
    public int SampleRateHz { get; set; } = 24000;
}
