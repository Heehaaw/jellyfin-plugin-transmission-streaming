namespace TransmissionStreaming.Infrastructure.Filesystem;

using Core.Filesystem;

/// <summary>Reads import metadata without opening or modifying the media payload.</summary>
internal sealed class PhysicalMediaFiles : IMediaFiles
{
    public bool HasLength(string path, long expectedLength)
    {
        try { return new FileInfo(path).Length == expectedLength; }
        catch (FileNotFoundException) { return false; }
        catch (DirectoryNotFoundException) { return false; }
    }
}
