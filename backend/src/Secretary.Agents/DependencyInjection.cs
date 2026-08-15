using Secretary.Agents.Realtime;
using Secretary.Agents.ServiceCatalog;
using Secretary.Agents.Tools;
using Secretary.Application.Abstractions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Secretary.Voice;

namespace Secretary.Agents;

public static class AgentsServiceCollectionExtensions
{
    public static IServiceCollection AddAgents(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AgentProviderOptions>(configuration.GetSection(AgentProviderOptions.SectionName));
        services.AddSingleton<IAgentFactory, AgentFactory>();

        // Overrides Application's no-op default â€” must be registered after AddApplicationServices()
        // in the composition root (see Program.cs) for this to win.
        services.AddSingleton<IAgentDirectoryChangeNotifier, AgentDirectoryChangeNotifier>();
        services.AddScoped<ITenantServiceCatalogCache, TenantServiceCatalogCache>();
        services.AddScoped<ITenantProviderDirectory, TenantProviderDirectory>();

        // What the order line reads mid-sentence: the catalogue and the delivery policy. Cached
        // for latency, not load — the caller hears the round trip.
        services.AddScoped<ITenantOrderDirectory, Orders.TenantOrderDirectory>();

        services.AddScoped<ClientTools>();
        services.AddScoped<ServiceCatalogTools>();
        services.AddScoped<AppointmentTools>();
        services.AddScoped<EscalationTools>();
        services.AddScoped<CallControlTools>();
        // Scoped to the call: the one order it placed, which is what gates cancelling.
        services.AddScoped<Orders.OrderCallSession>();
        services.AddScoped<OrderTools>();

        // Scoped to the call: which queued survey call this conversation is. Set before the
        // agent starts, not discovered by it — that is what makes the module outbound-shaped.
        services.AddScoped<Feedback.FeedbackCallSession>();
        services.AddScoped<FeedbackTools>();

        // One IAgentModule per module that can answer a phone, and no shared IList<AITool> any
        // more: a toolset belongs to a module, and a call always knows which module it is.
        services.AddScoped<IAgentModule, AppointmentAgentModule>();
        services.AddScoped<IAgentModule, OrdersAgentModule>();
        services.AddScoped<IAgentModule, FeedbackAgentModule>();
        services.AddScoped<AgentModuleRegistry>();

        services.AddScoped<AgentInstructionContext>();
        services.AddScoped<PhoneAgentConversationService>();

        // Provider-neutral: running a tool and adding up tokens are the same whichever model
        // asked. The sessions themselves are registered by their own provider packages.
        services.AddScoped<RealtimeToolInvoker>();
        services.AddScoped<RealtimeSessionResolver>();
        services.AddScoped<LiveVoiceCallOrchestrator>();

        return services;
    }
}

