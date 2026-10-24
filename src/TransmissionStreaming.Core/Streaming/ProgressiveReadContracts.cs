namespace TransmissionStreaming.Core.Streaming;

/// <summary>A refreshable view of a file, including availability of its torrent pieces.</summary>
public sealed record ProgressiveReadState(string? Path, long Length, long FileOffset, long PieceSize, byte[] Pieces, bool Complete, bool DiskWritesSafe);

/// <summary>Bounds memory, polling, and the time a stalled playback request can wait.</summary>
public sealed record ProgressiveReadOptions
{
    public int BufferSize { get; set; } = 128 * 1024;
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromMilliseconds(500);
    public TimeSpan StallTimeout { get; set; } = TimeSpan.FromMinutes(2);
    public bool PrioritizeMissingPieces { get; set; }
}
