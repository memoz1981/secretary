using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Secretary.Domain.Enums;
using Secretary.Voice.Abstractions;

namespace Secretary.Voice.Google;

public static class GoogleVoiceServiceCollectionExtensions
{
    public static IServiceCollection AddGeminiLive(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<GeminiLiveOptions>(configuration.GetSection(GeminiLiveOptions.SectionName));

        // Transient: a fresh WebSocket session per live call, never reused across calls.
        services.AddTransient<GeminiLiveSession>();

        // Scoped, not singleton: the session it builds depends on the request-scoped toolset.
        services.AddScoped<IRealtimeSessionFactory, GeminiLiveSessionFactory>();

        return services;
    }
}

public sealed class GeminiLiveSessionFactory : IRealtimeSessionFactory
{
    private readonly IServiceProvider _services;

    public GeminiLiveSessionFactory(IServiceProvider services) => _services = services;

    public bool CanServe(CallPipeline pipeline) => pipeline == CallPipeline.GeminiLive_3_1;

    public IRealtimeSession Create() => _services.GetRequiredService<GeminiLiveSession>();
}
