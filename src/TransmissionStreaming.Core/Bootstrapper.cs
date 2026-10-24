namespace TransmissionStreaming.Core;

using Microsoft.Extensions.DependencyInjection;

/// <summary>Composes the standalone torrent reader without depending on the Jellyfin host.</summary>
public static class Bootstrapper
{
    public static void Register(IServiceCollection services)
    {
        Transmission.Bootstrapper.Register(services);
        Streaming.Bootstrapper.Register(services);
    }
}
