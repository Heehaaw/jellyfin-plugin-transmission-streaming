namespace TransmissionStreaming.Infrastructure;

using Arr;
using Configuration;
using Discovery;
using Filesystem;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Persistence;
using Projection;
using Streaming;
using Transmission;

/// <summary>Composes external transports, persistence, and filesystem implementations behind Core contracts.</summary>
public static class Bootstrapper
{
    /// <summary>Adds the infrastructure services used by the application handlers and plugin adapter.</summary>
    public static IServiceCollection AddTransmissionStreamingInfrastructure(this IServiceCollection services)
    {
        services.TryAddSingleton<TimeProvider>(TimeProvider.System);
        return services
        .AddTransmissionTransport()
        .AddArrIntegration()
        .AddConfigurationDiscovery()
        .AddMediaFilesystem()
        .AddProgressiveStreaming()
        .AddProjectionFiles()
        .AddProjectionPersistence()
        .AddDiscoveryStatus();
    }
}
