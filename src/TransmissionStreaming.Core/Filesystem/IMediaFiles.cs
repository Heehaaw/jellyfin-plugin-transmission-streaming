namespace TransmissionStreaming.Core.Filesystem;

/// <summary>Checks that an imported file is present and still matches its recorded release length.</summary>
public interface IMediaFiles
{
    bool HasLength(string path, long expectedLength);
}
