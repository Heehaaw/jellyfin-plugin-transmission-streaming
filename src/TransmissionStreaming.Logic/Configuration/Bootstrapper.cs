namespace TransmissionStreaming.Logic.Configuration;

using Microsoft.Extensions.DependencyInjection;

/// <summary>Registers local configuration inference behind its application contract.</summary>
internal static class Bootstrapper
{
    public static IServiceCollection AddConfigurationLogic(this IServiceCollection services)
    {
        return services.AddSingleton<IMediaRootInference, MediaRootInference>();
    }
}
