namespace TransmissionStreaming.Core.Projection;

/// <summary>Filesystem projection of permanent streaming entries.</summary>
public interface IProjectionFiles
{
    string Root { get; }
    void EnsureReady();
    void WriteFiles(Projection entry);
}
