namespace TransmissionStreaming.Infrastructure.Configuration;

using Core.Configuration;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Composes read-only remote path discovery behind its Core contract.</summary>
internal static class Bootstrapper
{
    public static IServiceCollection AddConfigurationDiscovery(this IServiceCollection services)
    {
        return services.AddSingleton<IServicePathDiscovery, ServicePathDiscovery>();
    }
}
