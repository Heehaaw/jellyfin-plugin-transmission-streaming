namespace Jellyfin.Plugin.TransmissionStreaming.Discovery;

using Microsoft.Extensions.DependencyInjection;

/// <summary>Connects the discovery timer to Jellyfin's hosted lifecycle.</summary>
internal static class Bootstrapper
{
    public static IServiceCollection AddJellyfinDiscovery(this IServiceCollection services)
    {
        return services.AddHostedService<DiscoveryHostedService>();
    }
}
