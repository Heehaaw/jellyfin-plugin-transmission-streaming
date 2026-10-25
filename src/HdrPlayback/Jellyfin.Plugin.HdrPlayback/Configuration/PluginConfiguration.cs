namespace Jellyfin.Plugin.HdrPlayback.Configuration;

using MediaBrowser.Model.Plugins;

/// <summary>Retains Jellyfin's standard configuration lifecycle; HDR selection is per playback, never global.</summary>
public sealed class PluginConfiguration : BasePluginConfiguration
{
}
