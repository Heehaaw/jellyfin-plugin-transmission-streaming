namespace TransmissionStreaming.Infrastructure.Arr;

using Core.Arr;
using Discovery;
using Imports;
using Microsoft.Extensions.DependencyInjection;
using Transport;

/// <summary>Composes read-only arr discovery with its transport and import proof.</summary>
internal static class Bootstrapper
{
    public static IServiceCollection AddArrIntegration(this IServiceCollection services)
    {
        services.AddArrTransport();
        services.AddSingleton<IArrDownloadDiscovery, ArrDownloadDiscovery>();
        services.AddSingleton<IArrImportResolver, ArrImportResolver>();
        services.AddSingleton<IArrClient, ArrClient>();
        return services;
    }
}
