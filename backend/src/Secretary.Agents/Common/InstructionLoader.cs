namespace Secretary.Agents;

public static class InstructionLoader
{
    public static string Load(string agentName)
        => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Instructions", $"{agentName}.md"));
}
