namespace TransmissionStreaming.Core.Filesystem;

/// <summary>Provides metadata checks and offset reads for media files without changing their contents.</summary>
public interface IMediaFiles
{
    /// <summary>Checks whether a file currently exists at the given path.</summary>
    bool Exists(string path);

    /// <summary>Checks the file's current length without reading or modifying its contents, returning false when it is missing.</summary>
    bool HasLength(string path, long expectedLength);

    /// <summary>Reads bytes at a file-relative offset, returning zero when the file or its directory is missing.</summary>
    /// <remarks>The caller owns the buffer. Cancellation and failures other than a missing path are propagated.</remarks>
    ValueTask<int> ReadAsync(string path, Memory<byte> buffer, long offset, CancellationToken cancellationToken);
}
