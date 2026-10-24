namespace TransmissionStreaming.Infrastructure.Discovery;

using Core.Discovery;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Owns the discovery feature's implementation lifetimes.</summary>
internal static class Bootstrapper
{
    public static IServiceCollection AddDiscoveryStatus(this IServiceCollection services)
    {
        services.AddSingleton<IDiscoveryStatus, DiscoveryStatusStore>();
        return services;
    }
}
