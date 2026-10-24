namespace Jellyfin.Plugin.TransmissionStreaming.Configuration;

using global::TransmissionStreaming.Core.Configuration;

/// <summary>Adapts Jellyfin's mutable plugin settings to a fresh application snapshot.</summary>
internal sealed class OptionsProvider(IPluginSettings plugin) : IStreamingOptionsProvider
{
    public StreamingOptions Get()
    {
        var config = plugin.Configuration;
        return new StreamingOptions
        {
            Enabled = config.Enabled,
            StateDirectory = plugin.StateDirectory,
            TransmissionUrl = config.TransmissionUrl,
            TransmissionUsername = config.TransmissionUsername,
            TransmissionPassword = config.TransmissionPassword,
            RadarrUrl = config.RadarrUrl,
            RadarrApiKey = config.RadarrApiKey,
            SonarrUrl = config.SonarrUrl,
            SonarrApiKey = config.SonarrApiKey,
            RemoteMediaRoot = config.RemoteMediaRoot,
            LocalMediaRoot = config.LocalMediaRoot,
            ProjectionRoot = config.ProjectionRoot,
            JellyfinPublicUrl = config.JellyfinPublicUrl,
            PollSeconds = config.PollSeconds,
            PieceWaitSeconds = config.PieceWaitSeconds,
            PrioritizePlayback = config.PrioritizePlayback,
            ShowDownloadStatus = config.ShowDownloadStatus
        };
    }
}
