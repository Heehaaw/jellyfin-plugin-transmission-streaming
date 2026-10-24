namespace TransmissionStreaming.Infrastructure.Streaming;

using Core.Streaming;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Owns the streaming feature's implementation lifetimes.</summary>
internal static class Bootstrapper
{
    public static IServiceCollection AddProgressiveStreaming(this IServiceCollection services)
    {
        services.AddSingleton<IProgressiveReader, ProgressiveReader>();
        services.AddSingleton<IPlaybackSourceFactory, PlaybackSourceFactory>();
        services.AddSingleton<IStreamActivity, StreamActivity>();
        return services;
    }
}
