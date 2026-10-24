namespace TransmissionStreaming.Infrastructure.Streaming;

using Core.Configuration;
using Core.Projection;
using Filesystem;
using global::TransmissionStreaming.Core.Streaming;
using global::TransmissionStreaming.Core.Transmission;
using ProjectionEntry = Core.Projection.Projection;

/// <summary>Creates per-request readers while keeping upstream polling out of the byte-copy loop.</summary>
internal sealed class PlaybackSourceFactory(IStreamingOptionsProvider options, IProjectionStore store, ITransmissionClient transmission, IProjectionPaths paths) : IPlaybackSourceFactory
{
    public IProgressiveSource Create(ProjectionEntry entry)
    {
        var config = options.Get();
        var lookup = new CompletedPathLookup(entry.Id, store, paths, config.LocalMediaRoot);
        return new TransmissionFileSource(transmission,
            new TransmissionConnection(config.TransmissionUrl, config.TransmissionUsername, config.TransmissionPassword),
            entry.TorrentHash, entry.FileIndex,
            [new PathMapping(config.RemoteMediaRoot, config.LocalMediaRoot)], entry.Length, lookup.GetAsync);
    }

    private sealed class CompletedPathLookup(string id, IProjectionStore store, IProjectionPaths paths, string localRoot)
    {
        private DateTimeOffset checkedAt;
        private string? cached;

        public Task<string?> GetAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (DateTimeOffset.UtcNow - checkedAt >= TimeSpan.FromMilliseconds(500))
            {
                var saved = store.Get(id)?.CompletedPath;
                cached = saved is null ? null : paths.Map(saved, localRoot, localRoot);
                checkedAt = DateTimeOffset.UtcNow;
            }
            return Task.FromResult(cached);
        }
    }
}
