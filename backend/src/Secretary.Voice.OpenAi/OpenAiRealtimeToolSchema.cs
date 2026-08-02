#pragma warning disable OPENAI002 // OpenAI.Realtime is still an evolving/preview surface of the OpenAI SDK.
using Microsoft.Extensions.AI;
using OpenAI.Realtime;

namespace Secretary.Voice.OpenAi;

/// <summary>Converts the same AIFunction/AITool objects used everywhere else (see
/// PhoneAgentToolset) into the Realtime SDK's shape. The tool *definition* — name, description,
/// parameters — is provider-agnostic; only this adapter differs. Gemini has its own.</summary>
public static class OpenAiRealtimeToolSchema
{
    public static IEnumerable<RealtimeTool> FromTools(IEnumerable<AITool> tools)
        => tools.OfType<AIFunction>().Select(ToRealtimeTool);

    private static RealtimeTool ToRealtimeTool(AIFunction function) => new RealtimeFunctionTool(function.Name)
    {
        FunctionDescription = function.Description,
        FunctionParameters = BinaryData.FromString(function.JsonSchema.GetRawText()),
    };
}
