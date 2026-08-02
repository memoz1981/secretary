using Secretary.Agents;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Secretary.Agents.Tests;

public sealed class AgentFactoryTests
{
    [Fact]
    public void Create_returns_an_agent_for_OpenAi_without_making_a_live_call()
    {
        var options = Options.Create(new AgentProviderOptions
        {
            OpenAi = new AgentProviderOptions.OpenAiOptions { ApiKey = "test-key", Model = "gpt-5.6" },
        });
        var sut = new AgentFactory(options);

        var agent = sut.Create(AgentProvider.OpenAi, "You are a test agent.");

        agent.ShouldNotBeNull();
    }
}
