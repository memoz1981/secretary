using System.Text.Json;
using Google.GenAI.Types;
using Microsoft.Extensions.AI;

namespace Secretary.Voice.Google;

/// <summary>Converts the same AIFunction objects every other path uses into Gemini's shape.
///
/// Gemini takes one Tool carrying many FunctionDeclarations, where OpenAI takes one tool each —
/// the only structural difference. The parameter schema goes through ParametersJsonSchema rather
/// than the SDK's typed Schema, so the JSON schema AIFunction already produces is passed
/// through untouched instead of being re-modelled and quietly losing constraints.</summary>
public static class GeminiLiveToolSchema
{
    public static List<Tool> FromTools(IEnumerable<AITool> tools)
    {
        var declarations = tools
            .OfType<AIFunction>()
            .Select(ToDeclaration)
            .ToList();

        return declarations.Count == 0
            ? []
            : [new Tool { FunctionDeclarations = declarations }];
    }

    private static FunctionDeclaration ToDeclaration(AIFunction function) => new()
    {
        Name = function.Name,
        Description = function.Description,
        ParametersJsonSchema = JsonSerializer.Deserialize<JsonElement>(function.JsonSchema.GetRawText()),
    };
}
