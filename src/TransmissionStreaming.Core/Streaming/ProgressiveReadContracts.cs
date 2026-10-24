namespace TransmissionStreaming.Core.Streaming;

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

/// <summary>Streams byte ranges while respecting source availability and playback cancellation.</summary>
public interface IProgressiveReader
{
    Task CopyAsync(IProgressiveSource source, Stream destination, long offset, long count, ProgressiveReadOptions options, CancellationToken cancellationToken = default);
}
