namespace TransmissionStreaming.Logic.Streaming;

using Core.Arr;
using Core.Configuration;
using Core.Filesystem;
using Core.Projection;
using Core.Streaming;
using Mediator;
using Microsoft.Extensions.Options;
using Configuration;
using Core.Transmission;

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
        TimeProvider timeProvider) : IQueryHandler<Query, ProgressiveReadState>
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

            // Store and RPC checks follow the polling cadence, rather than every output chunk.
            // Nullable timestamps ensure the first lookup happens even when a test clock starts at zero.
            if (Expired(playback.CompletionCheckedAt))
            {
                var saved = store.Get(entry.Id)?.CompletedPath ?? entry.CompletedPath;
                var service = entry.Identity.Kind == MediaKind.Movie ? MediaService.Radarr : MediaService.Sonarr;
                playback.ImportedPath = saved is null ? null : await mediator.Send(new ValidateLocalMediaPath.Query(service, saved), cancellationToken).ConfigureAwait(false);
                playback.CompletionCheckedAt = timeProvider.GetTimestamp();
            }

            if (playback.ImportedPath is { } imported && mediaFiles.HasLength(imported, entry.Length))
            {
                return new ProgressiveReadState(imported, entry.Length, 0, 0, [], true, true);
            }

            if (Expired(playback.RefreshedAt))
            {
                var connection = Connection();
                playback.Torrent = await client.GetTorrentAsync(connection, entry.TorrentHash, cancellationToken).ConfigureAwait(false) ?? playback.Torrent;
                playback.Transmission = await client.GetSessionAsync(connection, cancellationToken).ConfigureAwait(false);
                playback.RefreshedAt = timeProvider.GetTimestamp();
            }

            var torrent = playback.Torrent;
            var session = playback.Transmission;
            if (torrent is null || session is null || entry.FileIndex < 0 || entry.FileIndex >= torrent.Files.Count)
            {
                return new ProgressiveReadState(null, entry.Length, 0, 0, [], false, true);
            }

            var file = torrent.Files[entry.FileIndex];
            if (file.Length != entry.Length)
            {
                throw new IOException("The torrent file changed length; refusing to stream a different release.");
            }

            var relative = file.Name.Replace('\\', '/');
            var remotePaths = new List<string> { torrent.DownloadDirectory.TrimEnd('/') + "/" + relative };
            if (session.IncompleteDirectoryEnabled)
            {
                remotePaths.Add(session.IncompleteDirectory.TrimEnd('/') + "/" + relative);
            }

            var path = await FindBackingFileAsync(remotePaths, session.RenamePartialFiles, cancellationToken).ConfigureAwait(false);
            byte[] pieces;
            try
            {
                pieces = Convert.FromBase64String(torrent.Pieces);
            }
            catch (FormatException exception)
            {
                throw new IOException("Transmission returned an invalid piece map.", exception);
            }

            // Transmission 4.1.3 flushes verified pieces before advertising them in sequential mode.
            // Otherwise a verified bit may still refer to bytes held in its application write cache.
            var safe = torrent.SequentialDownload || session.CacheSizeMiB == 0;
            return new ProgressiveReadState(path, entry.Length, torrent.GetFileOffset(entry.FileIndex), torrent.PieceSize, pieces, false, safe);
        }

        private bool Expired(long? checkedAt)
        {
            return checkedAt is not { } timestamp || timeProvider.GetElapsedTime(timestamp) >= RefreshInterval;
        }

        private TransmissionConnection Connection()
        {
            var config = options.CurrentValue;
            return new TransmissionConnection(config.TransmissionUrl, config.TransmissionUsername, config.TransmissionPassword);
        }

        private async Task<string?> FindBackingFileAsync(IEnumerable<string> remotePaths, bool renamePartialFiles, CancellationToken cancellationToken)
        {
            foreach (var remote in remotePaths)
            {
                var local = await mediator.Send(new MapServicePath.Query(MediaService.Transmission, remote), cancellationToken).ConfigureAwait(false);
                if (local is not null && mediaFiles.Exists(local))
                {
                    return local;
                }

                if (renamePartialFiles)
                {
                    var partial = await mediator.Send(new MapServicePath.Query(MediaService.Transmission, remote + ".part"), cancellationToken).ConfigureAwait(false);
                    if (partial is not null && mediaFiles.Exists(partial))
                    {
                        return partial;
                    }
                }
            }

            return null;
        }
    }
}
