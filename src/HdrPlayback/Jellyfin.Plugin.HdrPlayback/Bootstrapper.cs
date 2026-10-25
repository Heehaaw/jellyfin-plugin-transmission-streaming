namespace Jellyfin.Plugin.HdrPlayback;

using global::HdrPlayback.Core.Playback;
using global::HdrPlayback.Core.Transcoding;
using global::HdrPlayback.Infrastructure;
using global::HdrPlayback.Logic;
using Jellyfin.Plugin.HdrPlayback.Player;
using MediaBrowser.Controller;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Controller.Plugins;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Packaging;
using Playback;
using Transcoding;

/// <summary>Composes HDR processing without replacing Jellyfin's encoder executable or native stream endpoints.</summary>
public sealed class Bootstrapper : IPluginServiceRegistrator
{
    public void RegisterServices(IServiceCollection services, IServerApplicationHost applicationHost)
    {
        services.AddHdrPlaybackLogic().AddHdrPlaybackInfrastructure();
        services.AddSingleton<IPluginReleaseMetadata, PluginReleaseMetadata>();
        services.AddSingleton<HdrCapabilities>();
        services.AddScoped<HdrPlaybackFilter>();
        services.PostConfigure<MvcOptions>(options => options.Filters.AddService<HdrPlaybackFilter>());
        services.AddTransient<IStartupFilter, HdrUiStartupFilter>();
        var native = services.LastOrDefault(descriptor => descriptor.ServiceType == typeof(ITranscodeManager))
                     ?? throw new InvalidOperationException("The native Jellyfin transcode manager is not registered.");
        services.Remove(native);
        services.AddSingleton<ITranscodeManager>(provider => new HdrTranscodeManager(
                inner: ResolveNative(native, provider),
                sessions: provider.GetRequiredService<IHdrSessionStore>(),
                transformer: provider.GetRequiredService<IHdrCommandTransformer>()
            )
        );
    }

    private static ITranscodeManager ResolveNative(ServiceDescriptor native, IServiceProvider provider)
    {
        return (ITranscodeManager)(native.ImplementationInstance
               ?? native.ImplementationFactory?.Invoke(provider)
               ?? ActivatorUtilities.CreateInstance(provider, native.ImplementationType!));
    }
}
