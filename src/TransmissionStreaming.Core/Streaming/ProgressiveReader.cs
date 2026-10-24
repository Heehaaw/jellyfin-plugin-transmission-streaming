namespace TransmissionStreaming.Core.Streaming;

using System.Buffers;
using System.Diagnostics;


/// <summary>A refreshable view of a file, including availability of its torrent pieces.</summary>
public sealed record ProgressiveReadState(string? Path, long Length, long FileOffset, long PieceSize, byte[] Pieces, bool Complete, bool DiskWritesSafe);

/// <summary>Supplies refreshed file locations and piece maps during playback.</summary>
public interface IProgressiveSource
{
    /// <summary>Returns current file availability and location.</summary>
    Task<ProgressiveReadState> GetStateAsync(CancellationToken cancellationToken);
    /// <summary>Optionally moves the downloader's cursor toward a missing byte range.</summary>
    Task PrioritizeAsync(long fileOffset, CancellationToken cancellationToken);
}

/// <summary>Bounds memory, polling, and the time a stalled playback request can wait.</summary>
public sealed record ProgressiveReadOptions
{
    public int BufferSize { get; set; } = 128 * 1024;
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromMilliseconds(500);
    public TimeSpan StallTimeout { get; set; } = TimeSpan.FromMinutes(2);
    public bool PrioritizeMissingPieces { get; set; }
}

/// <summary>Streams verified byte ranges from an incomplete file without reading sparse gaps.</summary>
public sealed class ProgressiveReader
{
    /// <summary>Copies a fixed byte range, waiting for missing verified pieces and following refreshed backing paths.</summary>
    public async Task CopyAsync(IProgressiveSource source, Stream destination, long offset, long count, ProgressiveReadOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        if (options.BufferSize is < 4096 or > 1024 * 1024 || options.PollInterval <= TimeSpan.Zero || options.StallTimeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(options));
        var buffer = ArrayPool<byte>.Shared.Rent(options.BufferSize);
        var stalled = Stopwatch.StartNew();
        var position = offset;
        var end = checked(offset + count);
        long? prioritized = null;
        try
        {
            while (position < end)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var state = await source.GetStateAsync(cancellationToken).ConfigureAwait(false);
                if (end > state.Length) throw new ArgumentOutOfRangeException(nameof(count), "Requested range exceeds the media length.");
                if (!state.DiskWritesSafe) throw new IOException("Progressive reads require Transmission sequential download or a disabled write cache.");
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
                    stalled.Restart();
                    prioritized = null;
                    continue;
                }
                if (options.PrioritizeMissingPieces && prioritized != position)
                {
                    await source.PrioritizeAsync(position, cancellationToken).ConfigureAwait(false);
                    prioritized = position;
                }
                if (stalled.Elapsed >= options.StallTimeout) throw new TimeoutException("The requested media bytes did not become available before the playback stall deadline.");
                await Task.Delay(options.PollInterval, cancellationToken).ConfigureAwait(false);
            }
        }
        finally { ArrayPool<byte>.Shared.Return(buffer); }
    }
}
