namespace Jellyfin.Plugin.TransmissionStreaming.Configuration;

using global::TransmissionStreaming.Core.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

/// <summary>Connects Jellyfin-owned configuration to application options.</summary>
internal static class Bootstrapper
{
    public static IServiceCollection AddPluginConfiguration(this IServiceCollection services)
    {
        services.AddOptions<StreamingOptions>();
        return services
            .AddSingleton<IPluginSettings>(_ => Plugin.Instance ?? throw new InvalidOperationException("Plugin has not initialized."))
            .AddSingleton<IConfigureOptions<StreamingOptions>, ConfigureStreamingOptions>()
            .AddSingleton<IOptionsChangeTokenSource<StreamingOptions>, PluginOptionsChangeTokenSource>()
            .AddSingleton<IMediaLibraryLocations, JellyfinMediaLibraryLocations>()
            .AddHostedService<ConfigurationInitializer>();
    }
}
