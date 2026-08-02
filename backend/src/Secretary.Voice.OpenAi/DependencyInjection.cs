using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Secretary.Domain.Enums;
using Secretary.Voice.Abstractions;

namespace Secretary.Voice.OpenAi;

public static class OpenAiVoiceServiceCollectionExtensions
{
    public static IServiceCollection AddOpenAiRealtime(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<OpenAiRealtimeOptions>(configuration.GetSection(OpenAiRealtimeOptions.SectionName));

        // Transient: a fresh WebSocket session per live call, never reused across calls.
        services.AddTransient<OpenAiRealtimeSession>();

        // Scoped, not singleton: the session it builds depends on the request-scoped toolset,
        // and resolving that from the root provider throws.
        services.AddScoped<IRealtimeSessionFactory, OpenAiRealtimeSessionFactory>();

        return services;
    }
}

/// <summary>Serves the OpenAI realtime pipelines. Registered as one of several factories the
/// resolver asks in turn, so adding a provider never edits a switch statement.</summary>
public sealed class OpenAiRealtimeSessionFactory : IRealtimeSessionFactory
{
    private readonly IServiceProvider _services;

    public OpenAiRealtimeSessionFactory(IServiceProvider services) => _services = services;

    public bool CanServe(CallPipeline pipeline) => pipeline == CallPipeline.OpenAiRealtime_2_1;

    public IRealtimeSession Create() => _services.GetRequiredService<OpenAiRealtimeSession>();
}
