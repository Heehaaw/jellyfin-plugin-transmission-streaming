namespace Jellyfin.Plugin.TransmissionStreaming.Streaming;

using Microsoft.Extensions.DependencyInjection;

/// <summary>Registers bounded playback and its short-lived source lookups.</summary>
internal static class Bootstrapper
{
    public static void Register(IServiceCollection services)
    {
        services.AddSingleton<PlaybackSourceFactory>();
        services.AddSingleton<StreamActivity>();
    }
}
