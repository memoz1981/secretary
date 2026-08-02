using Microsoft.Extensions.AI;

namespace Secretary.Agents;

/// <summary>Text-based stand-in for the real phone call, while the actual OpenAI Realtime
/// API + telephony bridge is deferred (see backend/README.md). Single-turn per call —
/// multi-turn state across HTTP requests and the live audio session are follow-up work, not
/// something this stopgap tries to solve.</summary>
public sealed class PhoneAgentConversationService
{
    private readonly IAgentFactory _agentFactory;
    private readonly IList<AITool> _tools;
    private readonly AgentInstructionContext _instructionContext;

    public PhoneAgentConversationService(IAgentFactory agentFactory, IList<AITool> tools, AgentInstructionContext instructionContext)
    {
        _agentFactory = agentFactory;
        _tools = tools;
        _instructionContext = instructionContext;
    }

    public async Task<string> RespondAsync(string callerMessage, CancellationToken cancellationToken)
    {
        // The text path runs on OpenAI, so it reads OpenAI's instruction file — the speech rules
        // in the other one are about how a model sounds, which does not apply here at all.
        var instructions = _instructionContext.BuildPhoneAgentInstructions("openai");
        var agent = _agentFactory.Create(AgentProvider.OpenAi, instructions, _tools);
        var response = await agent.RunAsync(callerMessage, cancellationToken: cancellationToken);
        return response.Text;
    }
}
