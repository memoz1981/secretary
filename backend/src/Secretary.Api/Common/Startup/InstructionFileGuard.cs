using Secretary.Api.Voice;

namespace Secretary.Api.Startup;

/// <summary>Refuses to start when a dialable pipeline has no instruction file.
///
/// Each provider reads its own complete file — the two models mishear and misspeak differently,
/// and a shared file with per-provider patches meant fixing one by degrading the other. The cost
/// of separate files is that one can go missing, and the failure is ugly and late: the first
/// caller to pick that pipeline gets a DirectoryNotFoundException from inside the orchestrator
/// and hears silence. That happened once already, when the file moved and its copy-to-output
/// path did not follow.
///
/// There is deliberately no fallback to another provider's file. Answering a Gemini call with
/// OpenAI-tuned instructions would look like it worked and quietly poison every comparison
/// between them, which is the whole reason for running two.</summary>
public static class InstructionFileGuard
{
    public static void EnsureEveryDialablePipelineHasInstructions()
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "Instructions");

        var missing = VoicePipelineCatalog.All
            .Where(entry => entry.Enabled)
            .Select(entry => new { entry.Pipeline, entry.ProviderKey, Path = Path.Combine(directory, $"PhoneAgent.{entry.ProviderKey}.md") })
            .Where(candidate => !File.Exists(candidate.Path))
            .ToList();

        if (missing.Count == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            "These pipelines are dialable but have no instruction file: "
            + string.Join("; ", missing.Select(m => $"{m.Pipeline} expects {m.Path}"))
            + ". Add the file under Modules/<Module>/Instructions/ — the csproj copies "
            + "Modules/**/Instructions/*.md flat into Instructions/ — or set Enabled: false on "
            + "the catalogue entry so it cannot be dialled.");
    }
}
