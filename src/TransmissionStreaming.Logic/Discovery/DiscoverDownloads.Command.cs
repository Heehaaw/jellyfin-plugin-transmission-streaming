namespace TransmissionStreaming.Logic.Discovery;

using Mediator;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using TransmissionStreaming.Core.Arr;
using TransmissionStreaming.Core.Configuration;
using TransmissionStreaming.Core.Projection;
using TransmissionStreaming.Core.Transmission;
using TransmissionStreaming.Logic.Imports;
using TransmissionStreaming.Logic.Library;

/// <summary>Defines the DiscoverDownloads command and its application handler.</summary>
public static class DiscoverDownloads
{
    /// <summary>Discovers arr-owned downloads and reconciles managed playback entries.</summary>
    public sealed record Command : ICommand;

    /// <summary>Coordinates download discovery and import handoff without owning network or filesystem adapters.</summary>
    internal sealed class Handler(
        IOptionsMonitor<StreamingOptions> options,
        IProjectionStore store,
        ITransmissionClient transmission,
        IArrClient arr,
        IProjectionPaths paths,
        IProjectionFiles files,
        IProjectionLibrary library,
        IMediator mediator) : ICommandHandler<Command>
    {
        private const int SecretBytes = 32;

        public async ValueTask<Unit> Handle(Command command, CancellationToken cancellationToken)
        {
            var config = options.CurrentValue;
            if (!config.Enabled)
            {
                return Unit.Value;
            }

            files.EnsureReady();
            var connection = new TransmissionConnection(config.TransmissionUrl, config.TransmissionUsername, config.TransmissionPassword);
            var torrents = await transmission.GetTorrentsAsync(connection, cancellationToken).ConfigureAwait(false);
            var entries = store.All().ToDictionary(entry => entry.Id, StringComparer.Ordinal);
            var added = false;

            foreach (var torrent in torrents)
            {
                var selection = await mediator.Send(new SelectVideoFiles.Query(torrent.Files), cancellationToken).ConfigureAwait(false);
                foreach (var index in selection.PlayableIndexes)
                {
                    var file = torrent.Files[index];
                    if (entries.Values.Any(entry => entry.TorrentHash.Equals(torrent.Hash, StringComparison.OrdinalIgnoreCase) && entry.FileIndex == index))
                    {
                        continue;
                    }

                    var identity = await IdentifyAsync(config, torrent.Hash, file.Name, cancellationToken).ConfigureAwait(false);
                    if (identity is null || identity.Kind == MediaKind.Movie && index != selection.PrimaryMovieIndex)
                    {
                        continue;
                    }

                    var id = paths.Id(identity);
                    // A completed entry or an active earlier release keeps ownership of its stable identity.
                    if (entries.ContainsKey(id))
                    {
                        continue;
                    }

                    var entry = new Projection
                    {
                        Id = id,
                        Secret = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(SecretBytes)),
                        TorrentHash = torrent.Hash,
                        FileIndex = index,
                        SourceName = file.Name,
                        Length = file.Length,
                        Identity = identity,
                        StreamPath = paths.StreamPath(files.Root, identity)
                    };
                    files.WriteFiles(entry);
                    store.Save(entry);
                    entries.Add(id, entry);
                    added = true;
                }
            }

            foreach (var entry in entries.Values.ToArray())
            {
                var torrent = torrents.FirstOrDefault(item => item.Hash.Equals(entry.TorrentHash, StringComparison.OrdinalIgnoreCase));
                var file = torrent?.Files.ElementAtOrDefault(entry.FileIndex);
                entry.Progress = file is null ? entry.Progress : Math.Clamp((double)file.BytesCompleted / entry.Length, 0, 1);
                if (entry.CompletedPath is null)
                {
                    var imported = await mediator.Send(new ResolveImportedFile.Query(entry), cancellationToken).ConfigureAwait(false);
                    if (imported is not null)
                    {
                        entry.CompletedPath = imported;
                    }
                }

                if (await mediator.Send(new HandoffCompletedProjection.Command(entry, torrent is not null), cancellationToken).ConfigureAwait(false))
                {
                    entries.Remove(entry.Id);
                    continue;
                }

                if (await mediator.Send(new PruneOrphanedProjection.Command(entry, torrent is not null), cancellationToken).ConfigureAwait(false))
                {
                    entries.Remove(entry.Id);
                    continue;
                }

                // Import proof establishes completion even after the downloader has removed its progress data.
                if (entry.CompletedPath is not null)
                {
                    entry.Progress = 1;
                }

                entry.Status = entry.CompletedPath is not null && entry.HandoffPending ? "Waiting for Jellyfin handoff"
                    : entry.CompletedPath is not null ? "Complete"
                    : torrent is null ? "Download unavailable"
                    : !string.IsNullOrEmpty(torrent.ErrorString) ? "Download error"
                    : entry.Progress >= 1 ? "Waiting for import"
                    : $"Downloading {entry.Progress:P0}";
                files.WriteFiles(entry);
                store.Save(entry);
            }

            if (added)
            {
                await library.RefreshAsync(cancellationToken).ConfigureAwait(false);
            }

            foreach (var entry in entries.Values)
            {
                await mediator.Send(new ReconcileProjection.Command(entry), cancellationToken).ConfigureAwait(false);
            }

            return Unit.Value;
        }

        private async Task<MediaIdentity?> IdentifyAsync(StreamingOptions config, string hash, string filename, CancellationToken cancellationToken)
        {
            foreach (var kind in new[] { MediaKind.Movie, MediaKind.Episode })
            {
                var movie = kind == MediaKind.Movie;
                var url = movie ? config.RadarrUrl : config.SonarrUrl;
                var key = movie ? config.RadarrApiKey : config.SonarrApiKey;
                if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                var connection = new ArrConnection { Url = url, ApiKey = key };
                var identity = await arr.FindDownloadAsync(connection, kind, hash, filename, cancellationToken).ConfigureAwait(false);
                if (identity is not null)
                {
                    return identity;
                }
            }

            return null;
        }
    }
}
