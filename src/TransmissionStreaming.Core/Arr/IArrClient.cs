namespace TransmissionStreaming.Core.Arr;

/// <summary>Read-only ownership discovery and exact import resolution for Radarr and Sonarr.</summary>
public interface IArrClient
{
    Task<MediaIdentity?> FindDownloadAsync(ArrConnection connection, MediaKind kind, string hash, string filename, CancellationToken cancellationToken);
    Task<string?> FindImportedFileAsync(ArrConnection connection, MediaIdentity identity, long expectedSize, CancellationToken cancellationToken);
}
