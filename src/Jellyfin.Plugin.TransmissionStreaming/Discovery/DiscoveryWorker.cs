namespace Jellyfin.Plugin.TransmissionStreaming.Discovery;

using global::TransmissionStreaming.Arr;
using global::TransmissionStreaming.Core.Transmission;
using Jellyfin.Plugin.TransmissionStreaming;
using Jellyfin.Plugin.TransmissionStreaming.Library;
using Jellyfin.Plugin.TransmissionStreaming.Persistence;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;


/// <summary>Discovers arr-owned video files and follows their exact import into the permanent projection.</summary>
public sealed class DiscoveryWorker(
    Plugin plugin,
    ProjectionStore store,
    TransmissionClient transmission,
    ArrClient arr,
    MediaPaths paths,
    LibraryProjection library,
    ILogger<DiscoveryWorker> logger) : BackgroundService
{
    public string? LastFailure { get; private set; }
    public DateTimeOffset? LastSuccessfulPoll { get; private set; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (plugin.Configuration.Enabled)
            {
                try
                {
                    await PollAsync(stoppingToken).ConfigureAwait(false);
                    LastSuccessfulPoll = DateTimeOffset.UtcNow;
                    LastFailure = null;
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception exception)
                {
                    // Upstream exceptions can contain credential-bearing URLs. Report the failure type only.
                    LastFailure = exception.GetType().Name;
                    logger.LogWarning("Transmission Streaming poll failed ({FailureType}). Check connections and path mappings.", LastFailure);
                }
            }
            await Task.Delay(TimeSpan.FromSeconds(Math.Clamp(plugin.Configuration.PollSeconds, DiscoveryConstants.MinimumPollSeconds, DiscoveryConstants.MaximumPollSeconds)), stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task PollAsync(CancellationToken cancellationToken)
    {
        var config = plugin.Configuration;
        ValidateRoot(library.Root);
        var connection = new TransmissionConnection(config.TransmissionUrl, config.TransmissionUsername, config.TransmissionPassword);
        var torrents = await transmission.GetTorrentsAsync(connection, cancellationToken).ConfigureAwait(false);
        var entries = store.All().ToDictionary(entry => entry.Id, StringComparer.Ordinal);
        var added = false;

        foreach (var torrent in torrents)
        {
            for (var index = 0; index < torrent.Files.Count; index++)
            {
                var file = torrent.Files[index];
                if (!Playable(file.Name) || file.Length <= 0) continue;
                if (entries.Values.Any(entry => entry.TorrentHash.Equals(torrent.Hash, StringComparison.OrdinalIgnoreCase) && entry.FileIndex == index)) continue;
                var identity = await IdentifyAsync(torrent.Hash, file.Name, cancellationToken).ConfigureAwait(false);
                if (identity is null) continue;

                var id = paths.Id(identity);
                // One stable entry per movie/episode. Concurrent upgrades must not hijack an active release.
                if (entries.ContainsKey(id)) continue;
                var entry = new Projection
                {
                    Id = id,
                    Secret = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(DiscoveryConstants.SecretBytes)),
                    TorrentHash = torrent.Hash,
                    FileIndex = index,
                    SourceName = file.Name,
                    Length = file.Length,
                    Identity = identity,
                    StreamPath = paths.StreamPath(library.Root, identity)
                };
                library.WriteFiles(entry);
                store.Save(entry);
                entries.Add(id, entry);
                added = true;
            }
        }

        foreach (var entry in entries.Values)
        {
            var torrent = torrents.FirstOrDefault(item => item.Hash.Equals(entry.TorrentHash, StringComparison.OrdinalIgnoreCase));
            var file = torrent?.Files.ElementAtOrDefault(entry.FileIndex);
            entry.Progress = file is null ? entry.Progress : Math.Clamp((double)file.BytesCompleted / entry.Length, 0, 1);

            if (entry.CompletedPath is null)
            {
                var imported = await ResolveImportAsync(entry, cancellationToken).ConfigureAwait(false);
                if (imported is not null) entry.CompletedPath = imported;
            }
            entry.Status = entry.CompletedPath is not null ? "Complete"
                : torrent is null ? "Download unavailable"
                : !string.IsNullOrEmpty(torrent.ErrorString) ? "Download error"
                : entry.Progress >= 1 ? "Waiting for import"
                : $"Downloading {entry.Progress:P0}";
            library.WriteFiles(entry);
            store.Save(entry);
        }

        if (added) await library.RefreshAsync(cancellationToken).ConfigureAwait(false);
        foreach (var entry in entries.Values) await library.ReconcileAsync(entry, cancellationToken).ConfigureAwait(false);
    }

    public async Task<string?> ResolveImportAsync(Projection entry, CancellationToken cancellationToken)
    {
        var config = plugin.Configuration;
        if (entry.CompletedPath is { } completed)
        {
            // Revalidate the saved path on every playback; a replaced or deleted release is not interchangeable.
            var mapped = paths.Map(completed, config.LocalMediaRoot, config.LocalMediaRoot);
            return File.Exists(mapped) && new FileInfo(mapped).Length == entry.Length ? mapped : null;
        }
        var connection = Connection(entry.Identity.Kind);
        if (connection is null) return null;
        var remote = await arr.FindImportedFileAsync(connection, entry.Identity, entry.Length, cancellationToken).ConfigureAwait(false);
        if (remote is null) return null;
        var local = paths.Map(remote, config.RemoteMediaRoot, config.LocalMediaRoot);
        return File.Exists(local) && new FileInfo(local).Length == entry.Length ? local : null;
    }

    private async Task<MediaIdentity?> IdentifyAsync(string hash, string filename, CancellationToken cancellationToken)
    {
        foreach (var kind in new[] { MediaKind.Movie, MediaKind.Episode })
        {
            if (Connection(kind) is not { } connection) continue;
            var identity = await arr.FindDownloadAsync(connection, kind, hash, filename, cancellationToken).ConfigureAwait(false);
            if (identity is not null) return identity;
        }
        return null;
    }

    private ArrConnection? Connection(MediaKind kind)
    {
        var config = plugin.Configuration;
        var url = kind == MediaKind.Movie ? config.RadarrUrl : config.SonarrUrl;
        var key = kind == MediaKind.Movie ? config.RadarrApiKey : config.SonarrApiKey;
        return string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(key) ? null : new ArrConnection { Url = url, ApiKey = key };
    }

    private static bool Playable(string name)
    {
        var file = Path.GetFileNameWithoutExtension(name).ToLowerInvariant();
        return Path.GetExtension(name).ToLowerInvariant() is ".mkv" or ".mp4" or ".m4v" or ".avi" or ".ts" or ".webm" or ".mov"
            && file != "sample" && !file.EndsWith("-sample", StringComparison.Ordinal) && !file.EndsWith(".sample", StringComparison.Ordinal);
    }

    private static void ValidateRoot(string root)
    {
        var full = Path.GetFullPath(root);
        if (full == Path.GetPathRoot(full)) throw new InvalidOperationException("A dedicated projection directory is required.");
        var marker = Path.Combine(full, DiscoveryConstants.OwnershipMarker);
        if (Directory.Exists(full) && !File.Exists(marker) && Directory.EnumerateFileSystemEntries(full).Any())
            throw new InvalidOperationException("The projection directory must be empty on first use.");
        Directory.CreateDirectory(full);
        if (!File.Exists(marker)) File.WriteAllText(marker, Plugin.PluginId.ToString());
        Directory.CreateDirectory(Path.Combine(full, "movies"));
        Directory.CreateDirectory(Path.Combine(full, "shows"));
    }
}
