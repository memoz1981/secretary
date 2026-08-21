using Secretary.Application.Abstractions;
using Secretary.Application.Pricing;
using Secretary.Application.Services;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Secretary.Application;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining(typeof(ApplicationServiceCollectionExtensions));

        // Overridden by AddAgents()'s real cache-invalidating implementation when that's
        // registered afterwards in the composition root â€” Application works standalone
        // without it (e.g. no agent wired up at all) rather than requiring a null check
        // at every ServiceOfferingService call site.
        services.AddSingleton<IAgentDirectoryChangeNotifier, NullAgentDirectoryChangeNotifier>();

        // Stateless rate lookup over IOptions â€” nothing per-request about it. Left unconfigured
        // (Application used standalone, or a test), the rate card is empty and calls price at
        // zero; the composition root is what binds real rates and refuses to boot without them
        // for the model actually in use. See Program.cs.
        services.AddSingleton<TokenPricebook>();

        services.AddScoped<AuthService>();
        services.AddScoped<CallCostVisibility>();
        services.AddScoped<TenantService>();
        services.AddScoped<TenantInsightsService>();
        services.AddScoped<AccountService>();
        services.AddScoped<ProviderService>();
        services.AddScoped<ServiceOfferingService>();
        services.AddScoped<ClientService>();
        services.AddScoped<AppointmentService>();
        services.AddScoped<CallService>();
        services.AddScoped<OrderCallService>();
        services.AddScoped<SurveyService>();
        services.AddScoped<FeedbackCallService>();
        services.AddScoped<FeedbackSettingsService>();
        services.AddScoped<EscalationService>();
        services.AddScoped<DashboardService>();
        services.AddScoped<TenantModuleService>();
        services.AddScoped<BusinessHoursService>();
        services.AddScoped<CustomerIdentityService>();
        services.AddScoped<OrderService>();
        services.AddScoped<ProductService>();

        // Scoped so the module lookup happens once per request, however many times the
        // authorization policy and /me ask for it.
        services.AddScoped<ICurrentTenantModules, CurrentTenantModules>();

        return services;
    }
}


