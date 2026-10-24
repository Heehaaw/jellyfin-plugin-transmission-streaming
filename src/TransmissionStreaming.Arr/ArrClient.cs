namespace TransmissionStreaming.Arr;

using global::TransmissionStreaming.Arr.Discovery;
using global::TransmissionStreaming.Arr.Imports;


/// <summary>Read-only download discovery and verified import resolution for Radarr and Sonarr v3.</summary>
public sealed class ArrClient(ArrDownloadDiscovery discovery, ArrImportResolver imports)
{
    /// <summary>Finds the exact movie or episode associated with a torrent in the arr queue.</summary>
    public Task<MediaIdentity?> FindDownloadAsync(
        ArrConnection connection, MediaKind kind, string hash, string filename, CancellationToken cancellationToken)
        => discovery.FindDownloadAsync(connection, kind, hash, filename, cancellationToken);

    /// <summary>Resolves a completed file only after the same torrent's import is proven by history.</summary>
    public Task<string?> FindImportedFileAsync(
        ArrConnection connection, MediaIdentity identity, long expectedSize, CancellationToken cancellationToken)
        => imports.FindImportedFileAsync(connection, identity, expectedSize, cancellationToken);
}
