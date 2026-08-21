using Secretary.Application.Abstractions;
using Secretary.Application.Abstractions.Persistence;
using NodaTime;
using NodaTime.Text;

namespace Secretary.Agents;

/// <summary>Builds the phone agent's instructions with the per-call dynamic context appended —
/// today's date and time, above all. The model has no clock of its own: without this it guessed
/// at "today", which is how it ended up offering callers a slot that had already gone by.
/// Azerbaijan time is hardcoded (see AzerbaijanTime) — the model does no timezone math at all.</summary>
public sealed class AgentInstructionContext
{
    /// <summary>The placeholder every instruction file writes where the business's name belongs.
    ///
    /// ⚠ It was never substituted. Three instruction files told the agent to say
    /// "[tenant business name] adından zəng edirəm" and the agent, reading square brackets as an
    /// instruction to supply something, supplied whatever name was nearest in its context — on a
    /// real survey call it introduced itself as the questionnaire. The file read as if this
    /// worked, which is why nobody looked.</summary>
    private const string BusinessNamePlaceholder = "[tenant business name]";

    private static readonly LocalDateTimePattern LocalPattern =
        LocalDateTimePattern.CreateWithInvariantCulture("ddd yyyy-MM-dd HH:mm");

    private readonly IClock _clock;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentTenantProvider _currentTenant;

    public AgentInstructionContext(IClock clock, IUnitOfWork uow, ICurrentTenantProvider currentTenant)
    {
        _clock = clock;
        _uow = uow;
        _currentTenant = currentTenant;
    }

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
    public async Task<string> BuildPhoneAgentInstructionsAsync(
        string instructionName, string providerKey, CancellationToken cancellationToken)
    {
        var local = _clock.GetCurrentInstant().InZone(AzerbaijanTime.Zone).LocalDateTime;
        var instructions = InstructionLoader.Load($"{instructionName}.{providerKey}");

        return WithBusinessName(instructions, await BusinessNameAsync(cancellationToken)) +
               "\n\n## Right now\n\n" +
               $"- The current date and time is {LocalPattern.Format(local)} (Azerbaijan time).\n" +
               "- Every time you hear, say, pass to a tool, or read from a tool result is Azerbaijan " +
               "local time. There is no timezone conversion anywhere in your job — never convert, " +
               "never append Z, never think in UTC.\n" +
               "- Never offer or book a time that is already in the past.";
    }

    /// <summary>Substitutes the business's name, and says plainly what to do when there is none.
    ///
    /// The empty case matters more than it looks. Leaving the brackets in is what caused the
    /// problem in the first place — a model handed "[tenant business name]" will not read the
    /// brackets aloud, it will fill them in. So the placeholder is replaced either way, and when
    /// there is no name the replacement is an instruction not to claim one.</summary>
    internal static string WithBusinessName(string instructions, string? businessName)
        => instructions.Replace(
            BusinessNamePlaceholder,
            string.IsNullOrWhiteSpace(businessName)
                ? "the business (whose name you have NOT been given — never invent one, and never "
                  + "use a product, service or questionnaire name in its place)"
                : businessName.Trim(),
            StringComparison.Ordinal);

    private async Task<string?> BusinessNameAsync(CancellationToken cancellationToken)
    {
        if (_currentTenant.TenantId is not { } tenantId)
        {
            return null;
        }

        return (await _uow.Tenants.GetByIdAsync(tenantId, cancellationToken))?.Name;
    }
}
