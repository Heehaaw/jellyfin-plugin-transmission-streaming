namespace TransmissionStreaming.Core.Filesystem;

/// <summary>Checks that an imported file is present and still matches its recorded release length.</summary>
public interface IMediaFiles
{
    /// <summary>Checks the file's current length without reading or modifying its contents, returning false when it is missing.</summary>
    bool HasLength(string path, long expectedLength);
}
