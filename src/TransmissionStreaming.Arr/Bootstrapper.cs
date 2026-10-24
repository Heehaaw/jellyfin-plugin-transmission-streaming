namespace TransmissionStreaming.Arr;

using Discovery;
using Imports;
using Microsoft.Extensions.DependencyInjection;
using Transport;

/// <summary>Composes arr discovery and import resolution with their credential-safe transport.</summary>
public static class Bootstrapper
{
    public static void Register(IServiceCollection services)
    {
        Transport.Bootstrapper.Register(services);
        services.AddSingleton<ArrDownloadDiscovery>();
        services.AddSingleton<ArrImportResolver>();
        services.AddSingleton<ArrClient>();
    }
}
