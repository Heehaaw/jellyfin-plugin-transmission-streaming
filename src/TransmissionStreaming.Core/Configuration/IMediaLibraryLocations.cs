namespace TransmissionStreaming.Core.Configuration;

/// <summary>Exposes configured media-library folders as seen by the hosting media server.</summary>
public interface IMediaLibraryLocations
{
    /// <summary>Returns configured folder paths without moving files or creating library entries.</summary>
    IReadOnlyList<string> GetPaths();
}
