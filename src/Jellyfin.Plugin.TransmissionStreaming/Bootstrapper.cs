namespace Jellyfin.Plugin.TransmissionStreaming;

using Configuration;
using Discovery;
using global::TransmissionStreaming.Infrastructure;
using global::TransmissionStreaming.Logic;
using Library;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Public because Jellyfin discovers exported registrators; all services behind it remain internal.</summary>
public sealed class Bootstrapper : IPluginServiceRegistrator
{
    public void RegisterServices(IServiceCollection services, IServerApplicationHost applicationHost)
    {
        services
            .AddPluginConfiguration()
            .AddJellyfinLibrary()
            .AddTransmissionStreamingLogic()
            .AddTransmissionStreamingInfrastructure()
            .AddJellyfinDiscovery();
    }
}
