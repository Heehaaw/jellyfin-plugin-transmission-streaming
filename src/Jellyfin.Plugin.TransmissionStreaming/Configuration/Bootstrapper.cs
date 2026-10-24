namespace Jellyfin.Plugin.TransmissionStreaming.Configuration;

using global::TransmissionStreaming.Core.Configuration;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Connects Jellyfin-owned configuration to application options.</summary>
internal static class Bootstrapper
{
    public static IServiceCollection AddPluginConfiguration(this IServiceCollection services)
    {
        return services
            .AddSingleton(_ => Plugin.Instance ?? throw new InvalidOperationException("Plugin has not initialized."))
            .AddSingleton<IStreamingOptionsProvider, OptionsProvider>();
    }
}
