namespace TransmissionStreaming.Core.Arr;

/// <summary>Read-only ownership discovery and exact import resolution for Radarr and Sonarr.</summary>
public interface IArrClient
{
    /// <summary>Returns configured library root folders in the arr service's own filesystem namespace.</summary>
    Task<IReadOnlyList<string>> GetRootFoldersAsync(ArrConnection connection, CancellationToken cancellationToken);

    /// <summary>Checks movie or series library ownership, independently of the download queue.</summary>
    /// <remarks>Unknown identities are preserved. Provider identifiers also match titles re-added with a different internal ID. Failed or malformed responses propagate.</remarks>
    Task<bool> IsMediaTrackedAsync(ArrConnection connection, MediaIdentity identity, CancellationToken cancellationToken);

    /// <summary>Matches a torrent file to an arr queue identity, returning null when ownership cannot be resolved unambiguously.</summary>
    /// <param name="connection">The arr endpoint and credentials used only for read-only requests.</param>
    /// <param name="kind">Selects the Radarr movie or Sonarr episode ownership flow.</param>
    /// <param name="hash">The original Transmission torrent hash.</param>
    /// <param name="filename">The torrent-relative filename retained for import matching and episode identification.</param>
    /// <param name="cancellationToken">Cancels upstream requests without changing arr or the download.</param>
    Task<MediaIdentity?> FindDownloadAsync(ArrConnection connection, MediaKind kind, string hash, string filename, CancellationToken cancellationToken);

    /// <summary>Returns an imported remote path only when history and the current library file prove it belongs to the original download.</summary>
    /// <remarks>A missing, stale, or ambiguous import returns null; transport failures propagate to the caller.</remarks>
    Task<string?> FindImportedFileAsync(ArrConnection connection, MediaIdentity identity, long expectedSize, CancellationToken cancellationToken);
}
