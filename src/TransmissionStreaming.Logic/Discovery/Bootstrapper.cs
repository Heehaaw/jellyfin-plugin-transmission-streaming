namespace TransmissionStreaming.Logic.Discovery;

using Microsoft.Extensions.DependencyInjection;

/// <summary>Registers deterministic video selection for download discovery.</summary>
internal static class Bootstrapper
{
    public static IServiceCollection AddDiscoveryLogic(this IServiceCollection services)
    {
        return services.AddSingleton<IVideoFileSelection, VideoFileSelection>();
    }
}
