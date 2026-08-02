#pragma warning disable OPENAI002 // OpenAI.Realtime is still an evolving/preview surface of the OpenAI SDK.
using Microsoft.Extensions.AI;
using OpenAI.Realtime;

namespace Secretary.Agents.Realtime;

/// <summary>Converts the same AIFunction/AITool objects used everywhere else in this skill
/// (see Tools/PhoneAgentToolset.cs) into the Realtime SDK's RealtimeFunctionTool shape. The
/// tool *definition* (name, description, parameters) is provider-agnostic; only this adapter
/// and the surrounding transport differ from PhoneAgentConversationService's text-based
/// ChatClientAgent path.</summary>
public static class RealtimeToolSchema
{
    public static IEnumerable<RealtimeTool> FromTools(IEnumerable<AITool> tools)
        => tools.OfType<AIFunction>().Select(ToRealtimeTool);

    private static RealtimeTool ToRealtimeTool(AIFunction function) => new RealtimeFunctionTool(function.Name)
    {
        FunctionDescription = function.Description,
        FunctionParameters = BinaryData.FromString(function.JsonSchema.GetRawText()),
    };
}
