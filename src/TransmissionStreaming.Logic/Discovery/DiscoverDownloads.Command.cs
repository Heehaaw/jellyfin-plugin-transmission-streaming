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

    /// <summary>Coordinates discovery and import handoff without owning network or filesystem adapters.</summary>
    internal sealed class Handler(
        IOptionsMonitor<StreamingOptions> options,
        IProjectionStore store,
        ITransmissionClient transmission,
        IArrClient arr,
        IProjectionPaths paths,
        IProjectionFiles files,
        IProjectionLibrary library,
        IMediator mediator
    ) : ICommandHandler<Command>
    {
        private const int SecretBytes = 32;

        public async ValueTask<Unit> Handle(Command command, CancellationToken cancellationToken)
        {
            var configuration = options.CurrentValue;
            if (!configuration.Enabled)
            {
                return Unit.Value;
            }

            files.EnsureReady();
            var connection = new TransmissionConnection(configuration.TransmissionUrl, configuration.TransmissionUsername, configuration.TransmissionPassword);
            var torrents = await transmission.GetTorrentsAsync(connection, cancellationToken).ConfigureAwait(false);
            var entries = store.All().ToDictionary(entry => entry.Id, StringComparer.Ordinal);
            var added = await CreateMissingProjectionsAsync(configuration, torrents, entries, cancellationToken).ConfigureAwait(false);

            foreach (var entry in entries.Values.ToArray())
            {
                var retired = await RefreshProjectionAsync(entry, torrents, cancellationToken).ConfigureAwait(false);
                if (retired)
                {
                    entries.Remove(entry.Id);
                }
            }

            if (added)
            {
                await library.RefreshAsync(cancellationToken).ConfigureAwait(false);
            }

            foreach (var entry in entries.Values)
            {
                var reconcile = new ReconcileProjection.Command(entry);
                await mediator.Send(reconcile, cancellationToken).ConfigureAwait(false);
            }

            return Unit.Value;
        }

        private async Task<bool> CreateMissingProjectionsAsync(
            StreamingOptions configuration,
            IReadOnlyList<TorrentSnapshot> torrents,
            Dictionary<string, Projection> entries,
            CancellationToken cancellationToken
        )
        {
            var added = false;
            foreach (var torrent in torrents)
            {
                var selectionQuery = new SelectVideoFiles.Query(torrent.Files);
                var selection = await mediator.Send(selectionQuery, cancellationToken).ConfigureAwait(false);
                foreach (var index in selection.PlayableIndexes)
                {
                    var entry = await CreateProjectionAsync(configuration, torrent, index, selection.PrimaryMovieIndex, entries, cancellationToken).ConfigureAwait(false);
                    if (entry is null)
                    {
                        continue;
                    }

                    files.WriteFiles(entry);
                    store.Save(entry);
                    entries.Add(entry.Id, entry);
                    added = true;
                }
            }

            return added;
        }

        private async Task<Projection?> CreateProjectionAsync(
            StreamingOptions configuration,
            TorrentSnapshot torrent,
            int fileIndex,
            int? primaryMovieIndex,
            Dictionary<string, Projection> entries,
            CancellationToken cancellationToken
        )
        {
            var alreadyOwned = entries.Values.Any(entry => entry.TorrentHash.Equals(torrent.Hash, StringComparison.OrdinalIgnoreCase) && entry.FileIndex == fileIndex);
            if (alreadyOwned)
            {
                return null;
            }

            var file = torrent.Files[fileIndex];
            var identity = await IdentifyAsync(configuration, torrent.Hash, file.Name, cancellationToken).ConfigureAwait(false);
            if (identity is null || identity.Kind == MediaKind.Movie && fileIndex != primaryMovieIndex)
            {
                return null;
            }

            var projectionId = paths.Id(identity);
            // A completed entry or an active earlier release keeps ownership of its stable identity.
            if (entries.ContainsKey(projectionId))
            {
                return null;
            }

            var secretBytes = RandomNumberGenerator.GetBytes(SecretBytes);
            return new Projection
            {
                Id = projectionId,
                Secret = Convert.ToHexStringLower(secretBytes),
                TorrentHash = torrent.Hash,
                FileIndex = fileIndex,
                SourceName = file.Name,
                Length = file.Length,
                Identity = identity,
                StreamPath = paths.StreamPath(files.Root, identity)
            };
        }

        private async Task<MediaIdentity?> IdentifyAsync(StreamingOptions configuration, string hash, string filename, CancellationToken cancellationToken)
        {
            foreach (var kind in new[]
                     {
                         MediaKind.Movie, MediaKind.Episode
                     })
            {
                var isMovie = kind == MediaKind.Movie;
                var serviceUrl = isMovie ? configuration.RadarrUrl : configuration.SonarrUrl;
                var apiKey = isMovie ? configuration.RadarrApiKey : configuration.SonarrApiKey;
                if (string.IsNullOrWhiteSpace(serviceUrl) || string.IsNullOrWhiteSpace(apiKey))
                {
                    continue;
                }

                var connection = new ArrConnection
                {
                    Url = serviceUrl,
                    ApiKey = apiKey
                };
                var identity = await arr.FindDownloadAsync(connection, kind, hash, filename, cancellationToken).ConfigureAwait(false);
                if (identity is not null)
                {
                    return identity;
                }
            }

            return null;
        }

        private async Task<bool> RefreshProjectionAsync(Projection entry, IReadOnlyList<TorrentSnapshot> torrents, CancellationToken cancellationToken)
        {
            var torrent = torrents.FirstOrDefault(item => item.Hash.Equals(entry.TorrentHash, StringComparison.OrdinalIgnoreCase));
            var file = torrent?.Files.ElementAtOrDefault(entry.FileIndex);
            if (file is not null)
            {
                var completedFraction = (double)file.BytesCompleted / entry.Length;
                entry.Progress = Math.Clamp(completedFraction, 0, 1);
            }

            if (entry.CompletedPath is null)
            {
                var importQuery = new ResolveImportedFile.Query(entry);
                entry.CompletedPath = await mediator.Send(importQuery, cancellationToken).ConfigureAwait(false);
            }

            var handoff = new HandoffCompletedProjection.Command(entry, torrent is not null);
            if (await mediator.Send(handoff, cancellationToken).ConfigureAwait(false))
            {
                return true;
            }

            var prune = new PruneOrphanedProjection.Command(entry, torrent is not null);
            if (await mediator.Send(prune, cancellationToken).ConfigureAwait(false))
            {
                return true;
            }

            // Import proof establishes completion even after the downloader has removed its progress data.
            if (entry.CompletedPath is not null)
            {
                entry.Progress = 1;
            }

            entry.Status = GetDownloadStatus(entry, torrent);
            files.WriteFiles(entry);
            store.Save(entry);
            return false;
        }

        private static string GetDownloadStatus(Projection entry, TorrentSnapshot? torrent)
        {
            if (entry.CompletedPath is not null)
            {
                return entry.HandoffPending ? "Waiting for Jellyfin handoff" : "Complete";
            }

            if (torrent is null)
            {
                return "Download unavailable";
            }

            if (!string.IsNullOrEmpty(torrent.ErrorString))
            {
                return "Download error";
            }

            return entry.Progress >= 1 ? "Waiting for import" : $"Downloading {entry.Progress:P0}";
        }
    }
}
