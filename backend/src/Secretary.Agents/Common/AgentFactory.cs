using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OpenAI;

namespace Secretary.Agents;

internal sealed class AgentFactory : IAgentFactory
{
    private readonly AgentProviderOptions _options;

    public AgentFactory(IOptions<AgentProviderOptions> options) => _options = options.Value;

    public AIAgent Create(AgentProvider provider, string instructions, IList<AITool>? tools = null) => provider switch
    {
        AgentProvider.OpenAi => CreateOpenAiAgent(instructions, tools),
        _ => throw new ArgumentOutOfRangeException(nameof(provider)),
    };

    private AIAgent CreateOpenAiAgent(string instructions, IList<AITool>? tools)
    {
        var client = new OpenAIClient(_options.OpenAi.ApiKey);
        var chatClient = client.GetChatClient(_options.OpenAi.Model);
        return chatClient.AsIChatClient().AsAIAgent(instructions: instructions, tools: tools);
    }
}
