using NodaTime;
using NodaTime.Text;

namespace Secretary.Agents;

/// <summary>Builds the phone agent's instructions with the per-call dynamic context appended —
/// today's date and time, above all. The model has no clock of its own: without this it guessed
/// at "today", which is how it ended up offering callers a slot that had already gone by.
/// Azerbaijan time is hardcoded (see AzerbaijanTime) — the model does no timezone math at all.</summary>
public sealed class AgentInstructionContext
{
    private static readonly LocalDateTimePattern LocalPattern =
        LocalDateTimePattern.CreateWithInvariantCulture("ddd yyyy-MM-dd HH:mm");

    private readonly IClock _clock;

    public AgentInstructionContext(IClock clock) => _clock = clock;

    public string BuildPhoneAgentInstructions()
    {
        var local = _clock.GetCurrentInstant().InZone(AzerbaijanTime.Zone).LocalDateTime;

        return InstructionLoader.Load("PhoneAgent") +
               "\n\n## Right now\n\n" +
               $"- The current date and time is {LocalPattern.Format(local)} (Azerbaijan time).\n" +
               "- Every time you hear, say, pass to a tool, or read from a tool result is Azerbaijan " +
               "local time. There is no timezone conversion anywhere in your job — never convert, " +
               "never append Z, never think in UTC.\n" +
               "- Never offer or book a time that is already in the past.";
    }
}
