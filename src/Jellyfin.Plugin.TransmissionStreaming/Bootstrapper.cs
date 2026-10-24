namespace Jellyfin.Plugin.TransmissionStreaming;

using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Composes feature registrations into Jellyfin's service container.</summary>
public sealed class Bootstrapper : IPluginServiceRegistrator
{
    public void RegisterServices(IServiceCollection services, IServerApplicationHost applicationHost)
    {
        Configuration.Bootstrapper.Register(services);
        Persistence.Bootstrapper.Register(services);
        global::TransmissionStreaming.Core.Bootstrapper.Register(services);
        global::TransmissionStreaming.Arr.Bootstrapper.Register(services);
        Library.Bootstrapper.Register(services);
        Streaming.Bootstrapper.Register(services);
        Discovery.Bootstrapper.Register(services);
    }
}
