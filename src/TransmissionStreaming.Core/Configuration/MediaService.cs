namespace TransmissionStreaming.Core.Configuration;

/// <summary>Selects the configured service whose remote filesystem paths are being inspected.</summary>
public enum MediaService
{
    /// <summary>The Transmission downloader.</summary>
    Transmission,

    /// <summary>The Radarr movie manager.</summary>
    Radarr,

    /// <summary>The Sonarr episode manager.</summary>
    Sonarr
}
