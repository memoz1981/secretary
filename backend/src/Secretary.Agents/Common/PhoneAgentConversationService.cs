using Secretary.Domain.Enums;

namespace Secretary.Agents;

/// <summary>Text-based stand-in for the real phone call, while the actual OpenAI Realtime
/// API + telephony bridge is deferred (see backend/README.md). Single-turn per call —
/// multi-turn state across HTTP requests and the live audio session are follow-up work, not
/// something this stopgap tries to solve.</summary>
public sealed class PhoneAgentConversationService
{
    private readonly IAgentFactory _agentFactory;
    private readonly AgentModuleRegistry _agentModules;
    private readonly AgentInstructionContext _instructionContext;

    public PhoneAgentConversationService(
        IAgentFactory agentFactory, AgentModuleRegistry agentModules, AgentInstructionContext instructionContext)
    {
        _agentFactory = agentFactory;
        _agentModules = agentModules;
        _instructionContext = instructionContext;
    }

    public async Task<string> RespondAsync(string callerMessage, CancellationToken cancellationToken)
    {
        // Pinned to Appointment. A real line carries its module; this stopgap has no line, and
        // guessing would be worse than saying which one it speaks for.
        var module = _agentModules.For(Module.Appointment);

        // The text path runs on OpenAI, so it reads OpenAI's instruction file — the speech rules
        // in the other one are about how a model sounds, which does not apply here at all.
        var instructions = _instructionContext.BuildPhoneAgentInstructions(module.InstructionName, "openai");
        var agent = _agentFactory.Create(AgentProvider.OpenAi, instructions, module.BuildTools());
        var response = await agent.RunAsync(callerMessage, cancellationToken: cancellationToken);
        return response.Text;
    }
}
