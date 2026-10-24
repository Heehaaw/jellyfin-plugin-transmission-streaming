namespace TransmissionStreaming.Core.Streaming;

/// <summary>A refreshable view of a file, including availability of its torrent pieces.</summary>
public sealed record ProgressiveReadState(string? Path, long Length, long FileOffset, long PieceSize, byte[] Pieces, bool Complete, bool DiskWritesSafe);

/// <summary>Supplies refreshed file locations and piece maps during playback.</summary>
public interface IProgressiveSource
{
    /// <summary>Returns a current availability snapshot, allowing the backing path to change after import.</summary>
    /// <remarks>The source may cache upstream metadata between reads. A missing path or unavailable piece is not permission to serve sparse bytes.</remarks>
    Task<ProgressiveReadState> GetStateAsync(CancellationToken cancellationToken);

    /// <summary>Optionally moves the downloader's cursor toward a missing byte offset relative to this media file.</summary>
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

/// <summary>Streams byte ranges while respecting source availability and playback cancellation.</summary>
public interface IProgressiveReader
{
    /// <summary>Copies a fixed file-relative byte range, waiting for missing verified pieces and following refreshed backing paths.</summary>
    /// <remarks>The destination remains open. Cancellation and failures may leave a partially written response for the host to terminate.</remarks>
    /// <exception cref="TimeoutException">No bytes become available before the continuous-stall deadline.</exception>
    /// <exception cref="IOException">The source cannot guarantee that verified pieces have been written to disk, or a file read fails.</exception>
    Task CopyAsync(IProgressiveSource source, Stream destination, long offset, long count, ProgressiveReadOptions options, CancellationToken cancellationToken = default);
}
