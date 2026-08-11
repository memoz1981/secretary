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

    /// <summary>One complete instruction file per provider, not a shared file with per-provider
    /// patches. The two models mishear and misspeak differently — Gemini clips the first word of
    /// a time, OpenAI does not — and a rule written for one is dead weight or actively harmful in
    /// the other. There is no compiler and no test to catch a shared edit regressing the model
    /// you were not listening to, which is exactly why the files are kept whole and separate.
    ///
    /// They start as copies and diverge; duplication is the cheaper mistake here.
    ///
    /// One file per (module × provider), so the name carries both: Appointment.gemini.md. A line
    /// answers as one module, and an order call has nothing to learn from the booking rules.</summary>
    public string BuildPhoneAgentInstructions(string instructionName, string providerKey)
    {
        var local = _clock.GetCurrentInstant().InZone(AzerbaijanTime.Zone).LocalDateTime;

        return InstructionLoader.Load($"{instructionName}.{providerKey}") +
               "\n\n## Right now\n\n" +
               $"- The current date and time is {LocalPattern.Format(local)} (Azerbaijan time).\n" +
               "- Every time you hear, say, pass to a tool, or read from a tool result is Azerbaijan " +
               "local time. There is no timezone conversion anywhere in your job — never convert, " +
               "never append Z, never think in UTC.\n" +
               "- Never offer or book a time that is already in the past.";
    }
}
