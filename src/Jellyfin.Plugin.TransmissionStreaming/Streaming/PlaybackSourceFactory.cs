namespace Jellyfin.Plugin.TransmissionStreaming.Streaming;

using global::TransmissionStreaming.Core.Filesystem;
using global::TransmissionStreaming.Core.Streaming;
using global::TransmissionStreaming.Core.Transmission;
using Jellyfin.Plugin.TransmissionStreaming;
using Jellyfin.Plugin.TransmissionStreaming.Library;
using Jellyfin.Plugin.TransmissionStreaming.Persistence;


/// <summary>Creates per-request readers while keeping upstream polling out of the byte-copy loop.</summary>
public sealed class PlaybackSourceFactory(Plugin plugin, ProjectionStore store, TransmissionClient transmission, MediaPaths paths)
{
    public IProgressiveSource Create(Projection entry)
    {
        var config = plugin.Configuration;
        var lookup = new CompletedPathLookup(entry.Id, store, paths, config.LocalMediaRoot);
        return new TransmissionFileSource(transmission,
            new TransmissionConnection(config.TransmissionUrl, config.TransmissionUsername, config.TransmissionPassword),
            entry.TorrentHash, entry.FileIndex,
            [new PathMapping(config.RemoteMediaRoot, config.LocalMediaRoot)], entry.Length, lookup.GetAsync);
    }

    private sealed class CompletedPathLookup(string id, ProjectionStore store, MediaPaths paths, string localRoot)
    {
        private DateTimeOffset checkedAt;
        private string? cached;

        public Task<string?> GetAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (DateTimeOffset.UtcNow - checkedAt >= StreamingConstants.CompletedPathRefresh)
            {
                var saved = store.Get(id)?.CompletedPath;
                cached = saved is null ? null : paths.Map(saved, localRoot, localRoot);
                checkedAt = DateTimeOffset.UtcNow;
            }
            return Task.FromResult(cached);
        }
    }
}
