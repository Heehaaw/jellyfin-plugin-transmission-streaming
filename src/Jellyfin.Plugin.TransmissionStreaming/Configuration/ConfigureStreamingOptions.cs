namespace Jellyfin.Plugin.TransmissionStreaming.Configuration;

using global::TransmissionStreaming.Core.Configuration;
using Microsoft.Extensions.Options;

/// <summary>Adapts Jellyfin's mutable plugin settings to a fresh application snapshot.</summary>
internal sealed class ConfigureStreamingOptions(IPluginSettings plugin) : IConfigureOptions<StreamingOptions>
{
    public void Configure(StreamingOptions target)
    {
        var config = plugin.Configuration;
        target.Enabled = config.Enabled;
        target.StateDirectory = plugin.StateDirectory;
        target.TransmissionUrl = config.TransmissionUrl;
        target.TransmissionUsername = config.TransmissionUsername;
        target.TransmissionPassword = config.TransmissionPassword;
        target.RadarrUrl = config.RadarrUrl;
        target.RadarrApiKey = config.RadarrApiKey;
        target.SonarrUrl = config.SonarrUrl;
        target.SonarrApiKey = config.SonarrApiKey;
        target.PathMappings = config.PathMappings.Select(mapping => mapping with { }).ToArray();
        target.RemoteMediaRoot = config.RemoteMediaRoot;
        target.LocalMediaRoot = config.LocalMediaRoot;
        target.ProjectionRoot = config.ProjectionRoot;
        target.JellyfinPublicUrl = config.JellyfinPublicUrl;
        target.PollSeconds = config.PollSeconds;
        target.PieceWaitSeconds = config.PieceWaitSeconds;
        target.PrioritizePlayback = config.PrioritizePlayback;
        target.ShowDownloadStatus = config.ShowDownloadStatus;
    }
}
