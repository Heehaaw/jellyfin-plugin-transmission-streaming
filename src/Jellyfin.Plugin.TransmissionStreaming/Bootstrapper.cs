namespace Jellyfin.Plugin.TransmissionStreaming;

using Configuration;
using Discovery;
using global::TransmissionStreaming.Infrastructure;
using global::TransmissionStreaming.Logic.Discovery;
using Library;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Mediator;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Public because Jellyfin discovers exported registrators; all services behind it remain internal.</summary>
public sealed class Bootstrapper : IPluginServiceRegistrator
{
    public void RegisterServices(IServiceCollection services, IServerApplicationHost applicationHost)
    {
        services
            .AddPluginConfiguration()
            .AddJellyfinLibrary()
            .AddTransmissionStreamingInfrastructure()
            .AddJellyfinDiscovery();

        services.AddMediator((MediatorOptions options) =>
        {
            options.Namespace = "Jellyfin.Plugin.TransmissionStreaming.Dispatch";
            options.ServiceLifetime = ServiceLifetime.Singleton;
            options.GenerateTypesAsInternal = true;
            options.Assemblies = [typeof(DiscoverDownloads.Command)];
        });
    }
}
