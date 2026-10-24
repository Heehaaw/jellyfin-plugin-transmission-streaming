namespace Jellyfin.Plugin.TransmissionStreaming.Configuration;

/// <summary>Exposes the settings and state location owned by the Jellyfin plugin host.</summary>
internal interface IPluginSettings
{
    PluginConfiguration Configuration { get; }
    string StateDirectory { get; }
}
