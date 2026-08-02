#pragma warning disable OPENAI002 // OpenAI.Realtime is still an evolving/preview surface of the OpenAI SDK.
using System.ComponentModel;
using Secretary.Domain.ValueObjects;
using Secretary.Voice;
using Secretary.Voice.OpenAi;
using Microsoft.Extensions.AI;
using OpenAI.Realtime;
using Shouldly;
using Xunit;

namespace Secretary.Voice.Tests;

public sealed class RealtimeToolSchemaTests
{
    [Description("A test tool.")]
    private static string SampleTool([Description("A test parameter.")] string input) => input;

    [Fact]
    public void FromTools_converts_an_AIFunction_into_a_RealtimeFunctionTool()
    {
        var tools = new List<AITool> { AIFunctionFactory.Create(SampleTool) };

        var schema = OpenAiRealtimeToolSchema.FromTools(tools).ToList();

        schema.Count.ShouldBe(1);
        var tool = schema[0].ShouldBeOfType<RealtimeFunctionTool>();
        tool.FunctionName.ShouldBe(nameof(SampleTool));
        tool.FunctionDescription.ShouldBe("A test tool.");
    }
}
