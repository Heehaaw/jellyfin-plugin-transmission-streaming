namespace TransmissionStreaming.Infrastructure.Streaming;

using Core.Streaming;
using System.Buffers;

/// <summary>Streams verified byte ranges from an incomplete file without reading sparse gaps.</summary>
internal sealed class ProgressiveReader(TimeProvider timeProvider) : IProgressiveReader
{
    /// <summary>Copies a fixed byte range, waiting for missing verified pieces and following refreshed backing paths.</summary>
    public async Task CopyAsync(IProgressiveSource source, Stream destination, long offset, long count, ProgressiveReadOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        if (options.BufferSize is < 4096 or > 1024 * 1024 || options.PollInterval <= TimeSpan.Zero || options.StallTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(options));
        }

        var buffer = ArrayPool<byte>.Shared.Rent(options.BufferSize);
        var stalledAt = timeProvider.GetTimestamp();
        var position = offset;
        var end = checked(offset + count);
        long? prioritized = null;
        try
        {
            while (position < end)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var state = await source.GetStateAsync(cancellationToken).ConfigureAwait(false);
                if (end > state.Length)
                {
                    throw new ArgumentOutOfRangeException(nameof(count), "Requested range exceeds the media length.");
                }

                if (!state.DiskWritesSafe)
                {
                    throw new IOException("Progressive reads require Transmission sequential download or a disabled write cache.");
                }

                var requested = (int)Math.Min(end - position, options.BufferSize);
                var available = state.Complete ? requested : PieceMap.AvailableBytes(state.Pieces, state.PieceSize, checked(state.FileOffset + position), requested);
                var read = 0;
                if (available > 0 && state.Path is not null)
                {
                    try
                    {
                        using var handle = File.OpenHandle(state.Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, FileOptions.Asynchronous | FileOptions.RandomAccess);
                        read = await RandomAccess.ReadAsync(handle, buffer.AsMemory(0, available), position, cancellationToken).ConfigureAwait(false);
                    }
                    catch (FileNotFoundException) { }
                    catch (DirectoryNotFoundException) { }
                }
                if (read > 0)
                {
                    await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                    position += read;
                    stalledAt = timeProvider.GetTimestamp();
                    prioritized = null;
                    continue;
                }
                if (options.PrioritizeMissingPieces && prioritized != position)
                {
                    await source.PrioritizeAsync(position, cancellationToken).ConfigureAwait(false);
                    prioritized = position;
                }
                var stalledFor = timeProvider.GetElapsedTime(stalledAt);
                if (stalledFor >= options.StallTimeout)
                {
                    throw new TimeoutException("The requested media bytes did not become available before the playback stall deadline.");
                }

                // A long polling interval must not extend the final stall deadline.
                var remaining = options.StallTimeout - stalledFor;
                var delay = options.PollInterval < remaining ? options.PollInterval : remaining;
                await Task.Delay(delay, timeProvider, cancellationToken).ConfigureAwait(false);
            }
        }
        finally { ArrayPool<byte>.Shared.Return(buffer); }
    }
}
