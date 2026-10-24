namespace TransmissionStreaming.Infrastructure.Arr;

using global::TransmissionStreaming.Core.Arr;

using global::TransmissionStreaming.Infrastructure.Arr.Discovery;
using global::TransmissionStreaming.Infrastructure.Arr.Imports;
using global::TransmissionStreaming.Infrastructure.Arr.Transport;

/// <summary>Read-only download discovery and verified import resolution for Radarr and Sonarr v3.</summary>
internal sealed class ArrClient(IArrDownloadDiscovery discovery, IArrImportResolver imports, IArrTransport transport) : IArrClient
{
    /// <summary>Reads configured roots without assuming they share the downloader's mount layout.</summary>
    public async Task<IReadOnlyList<string>> GetRootFoldersAsync(ArrConnection connection, CancellationToken cancellationToken)
    {
        var response = await transport.GetAsync(connection, "rootfolder", cancellationToken).ConfigureAwait(false);
        return ArrJson.Array(response).Select(folder => ArrJson.Text(folder, "path"))
            .Where(path => !string.IsNullOrWhiteSpace(path)).Distinct(StringComparer.Ordinal).ToArray();
    }

    /// <summary>Finds the exact movie or episode associated with a torrent in the arr queue.</summary>
    public Task<MediaIdentity?> FindDownloadAsync(
        ArrConnection connection, MediaKind kind, string hash, string filename, CancellationToken cancellationToken)
    {
        return discovery.FindDownloadAsync(connection, kind, hash, filename, cancellationToken);
    }

    /// <summary>Resolves a completed file only after the same torrent's import is proven by history.</summary>
    public Task<string?> FindImportedFileAsync(
        ArrConnection connection, MediaIdentity identity, long expectedSize, CancellationToken cancellationToken)
    {
        return imports.FindImportedFileAsync(connection, identity, expectedSize, cancellationToken);
    }
}
