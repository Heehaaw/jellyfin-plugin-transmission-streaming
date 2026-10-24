namespace Jellyfin.Plugin.TransmissionStreaming.Discovery;

using Microsoft.Extensions.DependencyInjection;

/// <summary>Shares one discovery worker between the hosted lifecycle and status endpoint.</summary>
internal static class Bootstrapper
{
    public static void Register(IServiceCollection services)
    {
        services.AddSingleton<DiscoveryWorker>();
        services.AddHostedService(provider => provider.GetRequiredService<DiscoveryWorker>());
    }
}
