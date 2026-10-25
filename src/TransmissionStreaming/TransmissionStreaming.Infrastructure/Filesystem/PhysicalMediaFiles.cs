namespace TransmissionStreaming.Infrastructure.Filesystem;

using Core.Filesystem;

/// <summary>Owns physical metadata checks and shared offset reads while downloads or imports modify the file.</summary>
internal sealed class PhysicalMediaFiles : IMediaFiles
{
    public bool Exists(string path)
    {
        return File.Exists(path);
    }

    public bool HasLength(string path, long expectedLength)
    {
        try
        {
            return new FileInfo(path).Length == expectedLength;
        }
        catch (FileNotFoundException)
        {
            return false;
        }
        catch (DirectoryNotFoundException)
        {
            return false;
        }
    }

    public async ValueTask<int> ReadAsync(string path, Memory<byte> buffer, long offset, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            // A short-lived shared handle lets an import rename or remove the backing file between reads.
            using var handle = File.OpenHandle(
                path: path,
                mode: FileMode.Open,
                access: FileAccess.Read,
                share: FileShare.ReadWrite | FileShare.Delete,
                options: FileOptions.Asynchronous | FileOptions.RandomAccess
            );
            return await RandomAccess.ReadAsync(handle, buffer, offset, cancellationToken).ConfigureAwait(false);
        }
        catch (FileNotFoundException)
        {
            return 0;
        }
        catch (DirectoryNotFoundException)
        {
            return 0;
        }
    }
}
