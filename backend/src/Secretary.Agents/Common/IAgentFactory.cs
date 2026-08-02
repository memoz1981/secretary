using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Secretary.Agents;

public interface IAgentFactory
{
    AIAgent Create(AgentProvider provider, string instructions, IList<AITool>? tools = null);
}
