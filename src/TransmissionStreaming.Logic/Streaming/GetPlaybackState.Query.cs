namespace TransmissionStreaming.Logic.Streaming;

using Configuration;
using Core.Arr;
using Core.Configuration;
using Core.Filesystem;
using Core.Projection;
using Core.Streaming;
using Core.Transmission;
using Mediator;
using Microsoft.Extensions.Options;

/// <summary>Follows playback data through download and import paths without owning per-reader state.</summary>
public static class GetPlaybackState
{
    /// <summary>Refreshes one playback session's verified availability and current backing path.</summary>
    public sealed record Query(PlaybackSession Playback) : IQuery<ProgressiveReadState>;

    internal sealed class Handler(
        ITransmissionClient client,
        IOptionsMonitor<StreamingOptions> options,
        IProjectionStore store,
        IMediaFiles mediaFiles,
        IMediator mediator,
        TimeProvider timeProvider
    ) : IQueryHandler<Query, ProgressiveReadState>
    {
        private static readonly TimeSpan RefreshInterval = TimeSpan.FromMilliseconds(500);

        public async ValueTask<ProgressiveReadState> Handle(Query query, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var playback = query.Playback;
            var entry = playback.Entry;
            if (entry.Length <= 0)
            {
                throw new IOException("Torrent metadata does not contain a playable file length.");
            }

            var importedPath = await FindImportedPathAsync(playback, cancellationToken).ConfigureAwait(false);
            if (importedPath is not null)
            {
                return new ProgressiveReadState(importedPath, entry.Length, 0, 0, [], true, true);
            }

            await RefreshTorrentAsync(playback, cancellationToken).ConfigureAwait(false);
            var torrent = playback.Torrent;
            var session = playback.Transmission;
            if (torrent is null || session is null || entry.FileIndex < 0 || entry.FileIndex >= torrent.Files.Count)
            {
                return new ProgressiveReadState(null, entry.Length, 0, 0, [], false, true);
            }

            return await ReadDownloadStateAsync(entry, torrent, session, cancellationToken).ConfigureAwait(false);
        }

        private async Task<string?> FindImportedPathAsync(PlaybackSession playback, CancellationToken cancellationToken)
        {
            // Store checks follow the polling cadence, rather than every output chunk.
            if (Expired(playback.CompletionCheckedAt))
            {
                var entry = playback.Entry;
                var savedPath = store.Get(entry.Id)?.CompletedPath ?? entry.CompletedPath;
                playback.ImportedPath = null;
                if (savedPath is not null)
                {
                    var service = entry.Identity.Kind == MediaKind.Movie ? MediaService.Radarr : MediaService.Sonarr;
                    var validatePath = new ValidateLocalMediaPath.Query(service, savedPath);
                    playback.ImportedPath = await mediator.Send(validatePath, cancellationToken).ConfigureAwait(false);
                }

                playback.CompletionCheckedAt = timeProvider.GetTimestamp();
            }

            var importedPath = playback.ImportedPath;
            return importedPath is not null && mediaFiles.HasLength(importedPath, playback.Entry.Length) ? importedPath : null;
        }

        private bool Expired(long? checkedAt)
        {
            // A nullable timestamp forces the first lookup even when a test clock starts at zero.
            return checkedAt is not { } timestamp || timeProvider.GetElapsedTime(timestamp) >= RefreshInterval;
        }

        private async Task RefreshTorrentAsync(PlaybackSession playback, CancellationToken cancellationToken)
        {
            if (!Expired(playback.RefreshedAt))
            {
                return;
            }

            var configuration = options.CurrentValue;
            var connection = new TransmissionConnection(configuration.TransmissionUrl, configuration.TransmissionUsername, configuration.TransmissionPassword);
            var torrent = await client.GetTorrentAsync(connection, playback.Entry.TorrentHash, cancellationToken).ConfigureAwait(false);
            playback.Torrent = torrent ?? playback.Torrent;
            playback.Transmission = await client.GetSessionAsync(connection, cancellationToken).ConfigureAwait(false);
            playback.RefreshedAt = timeProvider.GetTimestamp();
        }

        private async Task<ProgressiveReadState> ReadDownloadStateAsync(
            Projection entry,
            TorrentSnapshot torrent,
            TransmissionSessionSnapshot session,
            CancellationToken cancellationToken
        )
        {
            var file = torrent.Files[entry.FileIndex];
            if (file.Length != entry.Length)
            {
                throw new IOException("The torrent file changed length; refusing to stream a different release.");
            }

            var relativePath = file.Name.Replace('\\', '/');
            var remotePaths = new List<string>
            {
                torrent.DownloadDirectory.TrimEnd('/') + "/" + relativePath
            };
            if (session.IncompleteDirectoryEnabled)
            {
                remotePaths.Add(session.IncompleteDirectory.TrimEnd('/') + "/" + relativePath);
            }

            var localPath = await FindBackingFileAsync(remotePaths, session.RenamePartialFiles, cancellationToken).ConfigureAwait(false);
            var pieces = DecodePieces(torrent.Pieces);
            var fileOffset = torrent.GetFileOffset(entry.FileIndex);

            // Sequential mode flushes verified pieces before advertising them; the write cache otherwise may hold them.
            var diskWritesSafe = torrent.SequentialDownload || session.CacheSizeMiB == 0;
            return new ProgressiveReadState(localPath, entry.Length, fileOffset, torrent.PieceSize, pieces, false, diskWritesSafe);
        }

        private async Task<string?> FindBackingFileAsync(IEnumerable<string> remotePaths, bool renamePartialFiles, CancellationToken cancellationToken)
        {
            foreach (var remotePath in remotePaths)
            {
                var mapCompletePath = new MapServicePath.Query(MediaService.Transmission, remotePath);
                var localPath = await mediator.Send(mapCompletePath, cancellationToken).ConfigureAwait(false);
                if (localPath is not null && mediaFiles.Exists(localPath))
                {
                    return localPath;
                }

                if (!renamePartialFiles)
                {
                    continue;
                }

                var mapPartialPath = new MapServicePath.Query(MediaService.Transmission, remotePath + ".part");
                var partialPath = await mediator.Send(mapPartialPath, cancellationToken).ConfigureAwait(false);
                if (partialPath is not null && mediaFiles.Exists(partialPath))
                {
                    return partialPath;
                }
            }

            return null;
        }

        private static byte[] DecodePieces(string encodedPieces)
        {
            try
            {
                return Convert.FromBase64String(encodedPieces);
            }
            catch (FormatException exception)
            {
                throw new IOException("Transmission returned an invalid piece map.", exception);
            }
        }
    }
}
