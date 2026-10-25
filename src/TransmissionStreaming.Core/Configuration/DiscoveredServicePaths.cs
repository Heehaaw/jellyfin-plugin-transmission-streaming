namespace TransmissionStreaming.Core.Configuration;

/// <summary>Directories reported by one service in that service's own filesystem namespace.</summary>
public sealed record DiscoveredServicePaths
{
    /// <summary>The downloader's default completed-download directory, when reported.</summary>
    public string? DownloadDirectory { get; init; }

    /// <summary>The enabled incomplete-download directory, when reported.</summary>
    public string? IncompleteDirectory { get; init; }

    /// <summary>Distinct default and per-torrent download directories.</summary>
    public IReadOnlyList<string> DownloadDirectories { get; init; } = [];

    /// <summary>Configured movie or series library roots reported by Radarr or Sonarr.</summary>
    public IReadOnlyList<string> LibraryRoots { get; init; } = [];
}
