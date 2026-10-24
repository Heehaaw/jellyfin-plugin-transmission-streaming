namespace TransmissionStreaming.Infrastructure;

using Arr;
using Discovery;
using Filesystem;
using Microsoft.Extensions.DependencyInjection;
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
        return services
        .AddTransmissionTransport()
        .AddArrIntegration()
        .AddMediaFilesystem()
        .AddProgressiveStreaming()
        .AddProjectionFiles()
        .AddProjectionPersistence()
        .AddDiscoveryStatus();
    }
}
