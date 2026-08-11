using Microsoft.Extensions.AI;
using Secretary.Agents;
using Secretary.Domain.Enums;
using Shouldly;
using Xunit;

namespace Secretary.Agents.Tests;

/// <summary>The registry is the seam a call passes through to find out whose business it is in.
/// Getting it wrong is not a compile error — it is an order call answered with the booking
/// toolset, which would look like it worked.</summary>
public sealed class AgentModuleRegistryTests
{
    [Fact]
    public void The_module_that_answers_a_line_is_the_one_registered_for_it()
    {
        var registry = new AgentModuleRegistry([Fake(Module.Appointment), Fake(Module.Order)]);

        registry.For(Module.Order).Key.ShouldBe(Module.Order);
    }

    /// <summary>The enum is longer than the list of modules that can answer a phone — the
    /// platform could grant Survey today. Falling back to another module's toolset would be
    /// worse than refusing.</summary>
    [Fact]
    public void A_module_granted_but_not_built_is_refused_rather_than_substituted()
    {
        var registry = new AgentModuleRegistry([Fake(Module.Appointment)]);

        registry.CanAnswer(Module.Survey).ShouldBeFalse();
        Should.Throw<InvalidOperationException>(() => registry.For(Module.Survey))
            .Message.ShouldContain("Survey");
    }

    [Fact]
    public void An_empty_registry_answers_nothing()
        => new AgentModuleRegistry([]).CanAnswer(Module.Appointment).ShouldBeFalse();

    private static IAgentModule Fake(Module key) => new FakeAgentModule(key);

    private sealed class FakeAgentModule : IAgentModule
    {
        public FakeAgentModule(Module key) => Key = key;

        public Module Key { get; }

        public string InstructionName => Key.ToString();

        public IList<AITool> BuildTools() => [];

        /// <summary>The registry test is about lookup, not logging.</summary>
        public Task LogCallAsync(CallLogEntry entry, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
