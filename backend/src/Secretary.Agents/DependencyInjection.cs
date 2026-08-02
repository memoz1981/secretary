using Secretary.Agents.Realtime;
using Secretary.Agents.ServiceCatalog;
using Secretary.Agents.Tools;
using Secretary.Application.Abstractions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Secretary.Agents;

public static class AgentsServiceCollectionExtensions
{
    public static IServiceCollection AddAgents(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AgentProviderOptions>(configuration.GetSection(AgentProviderOptions.SectionName));
        services.Configure<RealtimeOptions>(configuration.GetSection(RealtimeOptions.SectionName));
        services.AddSingleton<IAgentFactory, AgentFactory>();

        // Overrides Application's no-op default — must be registered after AddApplicationServices()
        // in the composition root (see Program.cs) for this to win.
        services.AddSingleton<IAgentDirectoryChangeNotifier, AgentDirectoryChangeNotifier>();
        services.AddScoped<ITenantServiceCatalogCache, TenantServiceCatalogCache>();
        services.AddScoped<ITenantProviderDirectory, TenantProviderDirectory>();

        services.AddScoped<ClientTools>();
        services.AddScoped<ServiceCatalogTools>();
        services.AddScoped<AppointmentTools>();
        services.AddScoped<EscalationTools>();
        services.AddScoped<CallControlTools>();
        services.AddScoped<IList<AITool>>(sp => PhoneAgentToolset.Build(
            sp.GetRequiredService<ClientTools>(),
            sp.GetRequiredService<ServiceCatalogTools>(),
            sp.GetRequiredService<AppointmentTools>(),
            sp.GetRequiredService<EscalationTools>(),
            sp.GetRequiredService<CallControlTools>()));

        services.AddScoped<AgentInstructionContext>();
        services.AddScoped<PhoneAgentConversationService>();

        // Transient: a fresh WebSocket session per live call, never reused across calls.
        services.AddTransient<RealtimeVoiceSession>();
        services.AddScoped<LiveVoiceCallOrchestrator>();

        return services;
    }
}
