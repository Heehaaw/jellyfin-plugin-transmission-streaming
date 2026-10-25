namespace Jellyfin.Plugin.TransmissionStreaming.Configuration;

using MediaBrowser.Model.Plugins;

/// <summary>Exposes the settings and state location owned by the Jellyfin plugin host.</summary>
internal interface IPluginSettings
{
    /// <summary>Notifies listeners after Jellyfin has persisted a configuration update.</summary>
    event EventHandler<BasePluginConfiguration>? ConfigurationChanged;

    PluginConfiguration Configuration { get; }

    string StateDirectory { get; }

    /// <summary>Persists updated host settings through Jellyfin's configuration lifecycle.</summary>
    void UpdateConfiguration(PluginConfiguration configuration);
}
