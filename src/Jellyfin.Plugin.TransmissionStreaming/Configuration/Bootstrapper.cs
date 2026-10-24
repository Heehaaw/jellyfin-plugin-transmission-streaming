namespace Jellyfin.Plugin.TransmissionStreaming.Configuration;

using Microsoft.Extensions.DependencyInjection;

/// <summary>Exposes the plugin instance that Jellyfin owns.</summary>
internal static class Bootstrapper
{
    public static void Register(IServiceCollection services)
    {
        services.AddSingleton(_ => Plugin.Instance ?? throw new InvalidOperationException("Plugin has not initialized."));
    }
}
