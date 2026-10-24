namespace TransmissionStreaming.Core.Configuration;

/// <summary>Exposes configured media-library folders as seen by the hosting media server.</summary>
public interface IMediaLibraryLocations
{
    /// <summary>Returns configured folder paths without moving files or creating library entries.</summary>
    IReadOnlyList<string> GetPaths();

    /// <summary>Returns movie folders for Radarr or show folders for Sonarr, as seen by the media server.</summary>
    IReadOnlyList<string> GetServicePaths(MediaService service);

    /// <summary>Returns all library folders, including download libraries that can identify Transmission mounts.</summary>
    IReadOnlyList<string> GetAllPaths();
}
