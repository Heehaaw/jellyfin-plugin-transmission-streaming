namespace TransmissionStreaming.Infrastructure.Streaming;

using Core.Configuration;
using Core.Projection;
using Filesystem;
using global::TransmissionStreaming.Core.Streaming;
using global::TransmissionStreaming.Core.Transmission;
using ProjectionEntry = Core.Projection.Projection;

/// <summary>Creates per-request readers while keeping upstream polling out of the byte-copy loop.</summary>
internal sealed class PlaybackSourceFactory(
    IStreamingOptionsProvider options,
    IProjectionStore store,
    ITransmissionClient transmission,
    IProjectionPaths paths,
    TimeProvider timeProvider) : IPlaybackSourceFactory
{
    public IProgressiveSource Create(ProjectionEntry entry)
    {
        var config = options.Get();
        var lookup = new CompletedPathLookup(entry.Id, store, paths, config.LocalMediaRoot, timeProvider);
        return new TransmissionFileSource(transmission, timeProvider,
            new TransmissionConnection(config.TransmissionUrl, config.TransmissionUsername, config.TransmissionPassword),
            entry.TorrentHash, entry.FileIndex,
            [new PathMapping(config.RemoteMediaRoot, config.LocalMediaRoot)], entry.Length, lookup.GetAsync);
    }

    private sealed class CompletedPathLookup(string id, IProjectionStore store, IProjectionPaths paths, string localRoot, TimeProvider timeProvider)
    {
        private long? checkedAt;
        private string? cached;

        public Task<string?> GetAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (checkedAt is not { } lastCheck || timeProvider.GetElapsedTime(lastCheck) >= TimeSpan.FromMilliseconds(500))
            {
                var saved = store.Get(id)?.CompletedPath;
                cached = saved is null ? null : paths.Map(saved, localRoot, localRoot);
                checkedAt = timeProvider.GetTimestamp();
            }
            return Task.FromResult(cached);
        }
    }
}
