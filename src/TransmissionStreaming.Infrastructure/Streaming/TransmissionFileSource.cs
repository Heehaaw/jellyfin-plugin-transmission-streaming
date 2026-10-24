namespace TransmissionStreaming.Infrastructure.Streaming;

using Core.Streaming;
using Filesystem;

using global::TransmissionStreaming.Core.Transmission;

/// <summary>Follows an individual Transmission file through incomplete, completed and imported paths.</summary>
internal sealed class TransmissionFileSource(
    ITransmissionClient client,
    TimeProvider timeProvider,
    TransmissionConnection connection,
    string hash,
    int fileIndex,
    IReadOnlyList<PathMapping> pathMappings,
    long expectedLength,
    Func<CancellationToken, Task<string?>>? completedPath = null) : IProgressiveSource
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMilliseconds(500);
    private TorrentSnapshot? _torrent;
    private TransmissionSessionSnapshot? _session;
    // A provider can start at timestamp zero; an unset cache must still refresh once.
    private long? _refreshedAt;
    private long? _completionCheckedAt;
    private string? _importedPath;

    /// <inheritdoc />
    public async Task<ProgressiveReadState> GetStateAsync(CancellationToken cancellationToken)
    {
        if (expectedLength <= 0)
        {
            throw new IOException("Torrent metadata does not contain a playable file length.");
        }
        // A file read happens per output chunk. Import reconciliation may call arr over HTTP,
        // so it follows the polling cadence rather than the much faster copy loop.
        if (completedPath is not null && (_completionCheckedAt is not { } completionCheckedAt || timeProvider.GetElapsedTime(completionCheckedAt) >= RefreshInterval))
        {
            _importedPath = await completedPath(cancellationToken).ConfigureAwait(false);
            _completionCheckedAt = timeProvider.GetTimestamp();
        }
        if (_importedPath is not null && IsAllowedLocalPath(_importedPath) && HasExpectedLength(_importedPath))
        {
            return new ProgressiveReadState(_importedPath, expectedLength, 0, 0, [], true, true);
        }

        if (_refreshedAt is not { } refreshedAt || timeProvider.GetElapsedTime(refreshedAt) >= RefreshInterval)
        {
            _torrent = await client.GetTorrentAsync(connection, hash, cancellationToken).ConfigureAwait(false) ?? _torrent;
            _session = await client.GetSessionAsync(connection, cancellationToken).ConfigureAwait(false);
            _refreshedAt = timeProvider.GetTimestamp();
        }
        var torrent = _torrent;
        var session = _session;
        if (torrent is null || session is null || fileIndex < 0 || fileIndex >= torrent.Files.Count)
        {
            return new ProgressiveReadState(null, expectedLength, 0, 0, [], false, true);
        }

        var file = torrent.Files[fileIndex];
        if (file.Length != expectedLength)
        {
            throw new IOException("The torrent file changed length; refusing to stream a different release.");
        }

        var relative = file.Name.Replace('\\', '/');
        var remotePaths = new List<string> { torrent.DownloadDirectory.TrimEnd('/') + "/" + relative };
        if (session.IncompleteDirectoryEnabled)
        {
            remotePaths.Add(session.IncompleteDirectory.TrimEnd('/') + "/" + relative);
        }

        string? path = null;
        foreach (var remote in remotePaths)
        {
            foreach (var mapping in pathMappings)
            {
                var local = mapping.Map(remote);
                if (local is not null && File.Exists(local)) { path = local; break; }
                local = mapping.Map(remote + ".part");
                if (session.RenamePartialFiles && local is not null && File.Exists(local)) { path = local; break; }
            }
            if (path is not null)
            {
                break;
            }
        }
        byte[] pieces;
        try { pieces = Convert.FromBase64String(torrent.Pieces); }
        catch (FormatException ex) { throw new IOException("Transmission returned an invalid piece map.", ex); }
        // 4.1.3 flushes pieces to the filesystem before advertising them in sequential mode.
        // Otherwise a verified bit can still refer to bytes only in its application write cache.
        var safe = torrent.SequentialDownload || session.CacheSizeMiB == 0;
        return new ProgressiveReadState(path, expectedLength, torrent.GetFileOffset(fileIndex), torrent.PieceSize, pieces, false, safe);
    }

    /// <inheritdoc />
    public async Task PrioritizeAsync(long fileOffset, CancellationToken cancellationToken)
    {
        if (_torrent is not { SequentialDownload: true, PieceSize: > 0 } torrent)
        {
            return;
        }

        var piece = checked((int)((torrent.GetFileOffset(fileIndex) + fileOffset) / torrent.PieceSize));
        await client.SetSequentialStartAsync(connection, hash, piece, cancellationToken).ConfigureAwait(false);
    }

    private static bool HasExpectedLength(string path, long length)
    {
        try { return new FileInfo(path).Length == length; }
        catch (FileNotFoundException) { return false; }
        catch (DirectoryNotFoundException) { return false; }
    }

    private bool HasExpectedLength(string path)
    {
        return HasExpectedLength(path, expectedLength);
    }

    private bool IsAllowedLocalPath(string path)
    {
        if (!Path.IsPathFullyQualified(path))
        {
            return false;
        }

        foreach (var mapping in pathMappings)
        {
            var relative = Path.GetRelativePath(mapping.LocalRoot, path).Replace(Path.DirectorySeparatorChar, '/');
            var mapped = mapping.Map(mapping.RemoteRoot.TrimEnd('/') + "/" + relative);
            if (mapped is not null && mapped.Equals(Path.GetFullPath(path), StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }
}
