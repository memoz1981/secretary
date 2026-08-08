using Secretary.Domain.Enums;

namespace Secretary.Agents;

/// <summary>Which modules can answer a phone, and what happens when one does.
///
/// Deliberately shorter than the Module enum, in the same way the front end's MODULE_REGISTRY
/// is: the platform can grant Reminder or Survey today because the grant model shipped before
/// those modules did, but nothing here can answer a call for them. Asking for one that cannot
/// throws rather than falling back — answering an order call with the appointment toolset would
/// look like it worked.</summary>
public sealed class AgentModuleRegistry
{
    private readonly IReadOnlyList<IAgentModule> _modules;

    public AgentModuleRegistry(IEnumerable<IAgentModule> modules) => _modules = modules.ToList();

    public IReadOnlyList<IAgentModule> All => _modules;

    public bool CanAnswer(Module module) => _modules.Any(m => m.Key == module);

    public IAgentModule For(Module module)
        => _modules.FirstOrDefault(m => m.Key == module)
           ?? throw new InvalidOperationException(
               $"No agent module answers calls for '{module}'. Register an IAgentModule for it, "
               + "or stop routing a line to it.");
}
