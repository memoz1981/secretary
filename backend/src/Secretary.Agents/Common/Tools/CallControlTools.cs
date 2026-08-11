using System.ComponentModel;

namespace Secretary.Agents.Tools;

/// <summary>The one tool that isn't a booking-system operation: it lets the model actually
/// hang up. Without it the line just stayed open after goodbyes, with Lamiya waiting for the
/// caller to speak again. The orchestrator intercepts this call by name (see
/// LiveVoiceCallOrchestrator) — the method body itself never runs on the live voice path;
/// it exists so the tool is in the schema the model sees, and for the text-based stand-in.</summary>
public sealed class CallControlTools
{
    [Description("Hangs up the phone call, once the caller's needs are handled or they say goodbye — never " +
                 "leave the line open after the conversation is over. Its result gives you the farewell to " +
                 "say: say exactly that and nothing else.")]
    public string EndCall() => "CALL_ENDED.";
}
