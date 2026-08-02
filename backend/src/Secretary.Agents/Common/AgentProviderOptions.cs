namespace Secretary.Agents;

public sealed class AgentProviderOptions
{
    public const string SectionName = "AgentProviders";

    public OpenAiOptions OpenAi { get; set; } = new();

    public sealed class OpenAiOptions
    {
        public string ApiKey { get; set; } = string.Empty;

        /// <summary>Verify this is still current before deploying — flagship model names in
        /// this space change every few months. GPT-5.6 ("Sol") was OpenAI's current flagship
        /// as of when this was built, having superseded GPT-5.4.</summary>
        public string Model { get; set; } = "gpt-5.6";
    }
}
